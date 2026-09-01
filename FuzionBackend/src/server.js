const express = require("express");
const crypto = require("crypto");
const { Pool } = require("pg");

const app = express();
const port = parseInt(process.env.PORT || "8080", 10);
const defaultGeminiModel = process.env.GEMINI_DEFAULT_MODEL || "gemini-3.5-flash-lite";
const customSearchCacheTtlSeconds = parseInt(process.env.CUSTOM_SEARCH_CACHE_TTL_SECONDS || "604800", 10);
const igdbCacheTtlSeconds = parseInt(process.env.IGDB_CACHE_TTL_SECONDS || "604800", 10);
const allowedCustomSearchParams = new Set([
  "q",
  "num",
  "fields",
  "searchType",
  "st",
  "tbm",
  "epq",
  "oq",
  "eq",
  "cr",
  "tbs",
  "safe",
  "filter",
  "gl",
  "hl",
  "siteSearch",
  "siteSearchFilter",
  "lr"
]);

let igdbTokenState = {
  accessToken: "",
  expiresAt: 0,
  inFlight: null
};
let databaseReadyPromise = null;
const dbPool = createDatabasePool();

app.disable("x-powered-by");
app.use(express.json({ limit: "256kb" }));
app.use(express.text({ type: ["text/plain", "application/apicalypse"], limit: "32kb" }));
app.use(express.urlencoded({ extended: false, limit: "1mb" }));

void initializeDatabase();

app.get("/health", (req, res) => {
  res.json({
    ok: true,
    service: "fuzion-backend",
    project: process.env.GOOGLE_CLOUD_PROJECT || null,
    region: process.env.GOOGLE_CLOUD_REGION || null,
    database: dbPool ? "configured" : "disabled"
  });
});

app.post("/gemini", handleAsync(async (req, res) => {
  const apiKey = getRequiredEnv("GEMINI_API_KEY");
  const model = resolveGeminiModel(typeof req.query.model === "string" ? req.query.model : "");
  const upstream = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(req.body || {})
    }
  );

  await relayResponse(upstream, res);
}));

app.get("/custom-search", handleAsync(async (req, res) => {
  const apiKey = getRequiredEnv("CUSTOM_SEARCH_API_KEY");
  const cx = getRequiredEnv("CUSTOM_SEARCH_CX");
  const query = buildCustomSearchQuery(req.query, apiKey, cx);
  const cacheKey = hashCacheKey("custom-search", query.toString());
  const cachedResponse = await getCachedJson("custom_search_cache", cacheKey);

  if (cachedResponse) {
    res.type("application/json").send(JSON.stringify(cachedResponse));
    return;
  }

  const upstream = await fetch(`https://www.googleapis.com/customsearch/v1/siterestrict?${query.toString()}`);
  const payloadText = await relayResponse(upstream, res);

  if (upstream.ok) {
    await upsertCachedJson("custom_search_cache", cacheKey, query.toString(), payloadText, customSearchCacheTtlSeconds);
  }
}));

app.all(["/production/v4/games", "/igdb/v4/games"], handleAsync(async (req, res) => {
  const clientId = getRequiredEnv("IGDB_TWITCH_CLIENT_ID");
  getRequiredEnv("IGDB_TWITCH_CLIENT_SECRET");

  const query = extractIgdbQuery(req);
  const cacheKey = hashCacheKey("igdb-games", query);
  const cachedResponse = await getCachedJson("igdb_search_cache", cacheKey);

  if (cachedResponse) {
    res.type("application/json").send(JSON.stringify(cachedResponse));
    return;
  }

  const upstream = await callIgdbGames(query, clientId);
  const payloadText = await relayResponse(upstream, res);

  if (upstream.ok) {
    await upsertCachedJson("igdb_search_cache", cacheKey, query, payloadText, igdbCacheTtlSeconds);
  }
}));

app.get("/get/main", handleAsync(async (req, res) => {
  const gameName = typeof req.query.gamename === "string" ? req.query.gamename : "";
  const falsePositive = parseBooleanQuery(req.query.falsepositive);
  const result = await getMetadataGame(gameName, falsePositive);

  res.json({
    status: !!result,
    result: result ? [result] : []
  });
}));

app.get("/get/program", handleAsync(async (req, res) => {
  const programName = typeof req.query.programname === "string" ? req.query.programname : "";
  const falsePositive = parseBooleanQuery(req.query.falsepositive);
  const result = await getMetadataProgram(programName, falsePositive);

  res.json({
    status: !!result,
    result: result ? [result] : []
  });
}));

app.post("/insert/main", handleAsync(async (req, res) => {
  const records = parsePostedJsonArray(req.body && req.body.data, "game list");
  const inserted = await upsertMetadataGames(records);

  res.json({
    ok: true,
    inserted
  });
}));

app.post("/insert/program", handleAsync(async (req, res) => {
  const records = parsePostedJsonArray(req.body && req.body.data, "program list");
  const inserted = await upsertMetadataPrograms(records);

  res.json({
    ok: true,
    inserted
  });
}));

app.use((req, res) => {
  res.status(404).json({ error: "Not found" });
});

app.use((error, req, res, next) => {
  const statusCode = error.statusCode || 500;
  console.error(error);
  res.status(statusCode).json({
    error: error.message || "Unexpected error"
  });
});

app.listen(port, () => {
  console.log(`Fuzion backend listening on port ${port}`);
});

function handleAsync(handler) {
  return (req, res, next) => {
    Promise.resolve(handler(req, res, next)).catch(next);
  };
}

function getRequiredEnv(name) {
  const value = process.env[name];

  if (!value || !value.trim()) {
    const error = new Error(`Missing required environment variable: ${name}`);
    error.statusCode = 503;
    throw error;
  }

  return value.trim();
}

function getOptionalEnv(name) {
  const value = process.env[name];
  return value && value.trim() ? value.trim() : "";
}

function splitCsv(value) {
  if (!value) {
    return [];
  }

  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function resolveGeminiModel(requestedModel) {
  const requested = requestedModel && requestedModel.trim() ? requestedModel.trim() : defaultGeminiModel;
  const allowedModels = splitCsv(process.env.GEMINI_ALLOWED_MODELS);

  if (allowedModels.length > 0 && !allowedModels.includes(requested)) {
    const error = new Error(`Model '${requested}' is not allowed.`);
    error.statusCode = 400;
    throw error;
  }

  return requested;
}

function clampInteger(value, min, max, fallbackValue) {
  const parsed = parseInt(value, 10);
  if (Number.isNaN(parsed)) {
    return fallbackValue;
  }

  return Math.min(max, Math.max(min, parsed));
}

function parseBooleanQuery(value) {
  return String(value || "0") === "1" || String(value || "false").toLowerCase() === "true";
}

function normalizeName(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replace(/\s+/g, " ");
}

function hashCacheKey(prefix, value) {
  return `${prefix}:${crypto.createHash("sha256").update(String(value || "")).digest("hex")}`;
}

function parsePostedJsonArray(raw, description) {
  if (!raw || !String(raw).trim()) {
    return [];
  }

  let parsed;
  try {
    parsed = JSON.parse(String(raw));
  } catch (error) {
    const parseError = new Error(`Invalid ${description} payload.`);
    parseError.statusCode = 400;
    throw parseError;
  }

  if (!Array.isArray(parsed)) {
    const arrayError = new Error(`Expected ${description} payload to be an array.`);
    arrayError.statusCode = 400;
    throw arrayError;
  }

  return parsed;
}

function createDatabasePool() {
  const connectionName = getOptionalEnv("CLOUD_SQL_CONNECTION_NAME");
  const database = getOptionalEnv("POSTGRES_DB");
  const user = getOptionalEnv("POSTGRES_USER");
  const password = getOptionalEnv("POSTGRES_PASSWORD");

  if (!connectionName || !database || !user || !password) {
    return null;
  }

  return new Pool({
    host: `/cloudsql/${connectionName}`,
    database,
    user,
    password,
    max: 5,
    idleTimeoutMillis: 30000,
    connectionTimeoutMillis: 10000
  });
}

async function initializeDatabase() {
  if (!dbPool) {
    return false;
  }

  if (databaseReadyPromise) {
    return databaseReadyPromise;
  }

  databaseReadyPromise = (async () => {
    const client = await dbPool.connect();
    try {
      await client.query(`
        create table if not exists metadata_games (
          id bigserial primary key,
          normalized_name text not null unique,
          gamename text not null,
          canonical_name text,
          iconlink text,
          exename text,
          falsepositive boolean not null default false,
          iconrelevance integer not null default 0,
          source text not null default 'manual',
          metadata_json jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create table if not exists metadata_programs (
          id bigserial primary key,
          normalized_name text not null unique,
          name text not null,
          iconlink text,
          exename text,
          falsepositive boolean not null default false,
          metadata_json jsonb not null default '{}'::jsonb,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create table if not exists custom_search_cache (
          cache_key text primary key,
          query_text text not null,
          response_json jsonb not null,
          expires_at timestamptz not null,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create table if not exists igdb_search_cache (
          cache_key text primary key,
          query_text text not null,
          response_json jsonb not null,
          expires_at timestamptz not null,
          created_at timestamptz not null default now(),
          updated_at timestamptz not null default now()
        );

        create index if not exists idx_metadata_games_falsepositive on metadata_games(falsepositive);
        create index if not exists idx_metadata_programs_falsepositive on metadata_programs(falsepositive);
        create index if not exists idx_custom_search_cache_expires_at on custom_search_cache(expires_at);
        create index if not exists idx_igdb_search_cache_expires_at on igdb_search_cache(expires_at);
      `);
      return true;
    } finally {
      client.release();
    }
  })().catch((error) => {
    databaseReadyPromise = null;
    console.error("Database initialization failed", error);
    return false;
  });

  return databaseReadyPromise;
}

async function withDatabase(callback, fallbackValue) {
  if (!dbPool) {
    return fallbackValue;
  }

  const ready = await initializeDatabase();
  if (!ready) {
    return fallbackValue;
  }

  const client = await dbPool.connect();
  try {
    return await callback(client);
  } catch (error) {
    console.error("Database operation failed", error);
    return fallbackValue;
  } finally {
    client.release();
  }
}

async function getCachedJson(tableName, cacheKey) {
  return withDatabase(async (client) => {
    const result = await client.query(
      `select response_json from ${tableName} where cache_key = $1 and expires_at > now()`,
      [cacheKey]
    );

    return result.rows.length > 0 ? result.rows[0].response_json : null;
  }, null);
}

async function upsertCachedJson(tableName, cacheKey, queryText, payloadText, ttlSeconds) {
  return withDatabase(async (client) => {
    const json = JSON.parse(payloadText);
    await client.query(
      `
        insert into ${tableName} (cache_key, query_text, response_json, expires_at, updated_at)
        values ($1, $2, $3::jsonb, now() + make_interval(secs => $4), now())
        on conflict (cache_key) do update
        set query_text = excluded.query_text,
            response_json = excluded.response_json,
            expires_at = excluded.expires_at,
            updated_at = now()
      `,
      [cacheKey, queryText, JSON.stringify(json), ttlSeconds]
    );
    return true;
  }, false);
}

async function getMetadataGame(gameName, falsePositive) {
  const normalized = normalizeName(gameName);
  if (!normalized) {
    return null;
  }

  return withDatabase(async (client) => {
    const result = await client.query(
      `
        select id, gamename, iconlink, exename, falsepositive, iconrelevance
        from metadata_games
        where normalized_name = $1 and falsepositive = $2
        limit 1
      `,
      [normalized, falsePositive]
    );

    if (result.rows.length === 0) {
      return null;
    }

    const row = result.rows[0];
    return {
      id: Number(row.id),
      gamename: row.gamename,
      iconlink: row.iconlink || "",
      exename: row.exename || "",
      falsepositive: row.falsepositive,
      iconrelevance: Number(row.iconrelevance) || 0
    };
  }, null);
}

async function getMetadataProgram(programName, falsePositive) {
  const normalized = normalizeName(programName);
  if (!normalized) {
    return null;
  }

  return withDatabase(async (client) => {
    const result = await client.query(
      `
        select id, name, iconlink, exename, falsepositive
        from metadata_programs
        where normalized_name = $1 and falsepositive = $2
        limit 1
      `,
      [normalized, falsePositive]
    );

    if (result.rows.length === 0) {
      return null;
    }

    const row = result.rows[0];
    return {
      id: Number(row.id),
      name: row.name,
      iconlink: row.iconlink || "",
      exename: row.exename || "",
      falsepositive: row.falsepositive
    };
  }, null);
}

async function upsertMetadataGames(records) {
  if (!Array.isArray(records) || records.length === 0) {
    return 0;
  }

  return withDatabase(async (client) => {
    let inserted = 0;
    for (const record of records) {
      const gameName = String(record.gameName || record.gamename || "").trim();
      if (!gameName) {
        continue;
      }

      const normalized = normalizeName(gameName);
      const iconLink = String(record.iconLink || record.iconlink || "").trim();
      const exeName = String(record.exeName || record.exename || "").trim();
      const iconRelevance = clampInteger(record.iconRelevance || record.iconrelevance, 0, 100, 0);

      await client.query(
        `
          insert into metadata_games
            (normalized_name, gamename, canonical_name, iconlink, exename, falsepositive, iconrelevance, source, updated_at)
          values ($1, $2, $2, nullif($3, ''), nullif($4, ''), false, $5, 'desktop-push', now())
          on conflict (normalized_name) do update
          set gamename = excluded.gamename,
              canonical_name = coalesce(metadata_games.canonical_name, excluded.canonical_name),
              iconlink = case
                when excluded.iconrelevance >= metadata_games.iconrelevance and excluded.iconlink is not null then excluded.iconlink
                else metadata_games.iconlink
              end,
              exename = case
                when excluded.exename is not null then excluded.exename
                else metadata_games.exename
              end,
              iconrelevance = case
                when excluded.iconrelevance > metadata_games.iconrelevance and excluded.iconlink is not null then excluded.iconrelevance
                else metadata_games.iconrelevance
              end,
              source = 'desktop-push',
              updated_at = now()
        `,
        [normalized, gameName, iconLink || null, exeName || null, iconRelevance]
      );
      inserted += 1;
    }

    return inserted;
  }, 0);
}

async function upsertMetadataPrograms(records) {
  if (!Array.isArray(records) || records.length === 0) {
    return 0;
  }

  return withDatabase(async (client) => {
    let inserted = 0;
    for (const record of records) {
      const name = String(record.name || record.programName || "").trim();
      if (!name) {
        continue;
      }

      const normalized = normalizeName(name);
      const iconLink = String(record.iconLink || record.iconlink || "").trim();
      const exeName = String(record.exeName || record.exename || "").trim();

      await client.query(
        `
          insert into metadata_programs
            (normalized_name, name, iconlink, exename, falsepositive, updated_at)
          values ($1, $2, nullif($3, ''), nullif($4, ''), false, now())
          on conflict (normalized_name) do update
          set name = excluded.name,
              iconlink = coalesce(excluded.iconlink, metadata_programs.iconlink),
              exename = coalesce(excluded.exename, metadata_programs.exename),
              updated_at = now()
        `,
        [normalized, name, iconLink || null, exeName || null]
      );
      inserted += 1;
    }

    return inserted;
  }, 0);
}

function buildCustomSearchQuery(queryObject, apiKey, cx) {
  const searchParams = new URLSearchParams();

  for (const [key, rawValue] of Object.entries(queryObject || {})) {
    if (!allowedCustomSearchParams.has(key)) {
      continue;
    }

    if (Array.isArray(rawValue)) {
      rawValue.forEach((value) => searchParams.append(key, String(value)));
    } else if (rawValue !== undefined && rawValue !== null && rawValue !== "") {
      searchParams.set(key, String(rawValue));
    }
  }

  if (!searchParams.get("q")) {
    const error = new Error("Missing required custom search query parameter: q");
    error.statusCode = 400;
    throw error;
  }

  searchParams.set("num", String(clampInteger(searchParams.get("num"), 1, 10, 5)));
  searchParams.set("key", apiKey);
  searchParams.set("cx", cx);

  return searchParams;
}

function extractIgdbQuery(req) {
  if (typeof req.query.query === "string" && req.query.query.trim()) {
    return req.query.query.trim();
  }

  if (typeof req.body === "string" && req.body.trim()) {
    return req.body.trim();
  }

  if (req.body && typeof req.body === "object" && typeof req.body.query === "string" && req.body.query.trim()) {
    return req.body.query.trim();
  }

  return buildIgdbQuery(toSearchParams(req.query));
}

function buildIgdbQuery(searchParams) {
  const fields = (searchParams.get("fields") || "name").trim();
  const clauses = [`fields ${fields};`];

  if (searchParams.get("where")) {
    clauses.push(`where ${searchParams.get("where")};`);
  }

  if (searchParams.get("search")) {
    clauses.push(`search \"${escapeIgdbString(searchParams.get("search"))}\";`);
  }

  if (searchParams.get("sort")) {
    clauses.push(`sort ${searchParams.get("sort")};`);
  }

  clauses.push(`limit ${clampInteger(searchParams.get("limit"), 1, 50, 5)};`);

  if (searchParams.get("offset")) {
    clauses.push(`offset ${clampInteger(searchParams.get("offset"), 0, 500, 0)};`);
  }

  return clauses.join(" ");
}

function toSearchParams(queryObject) {
  const params = new URLSearchParams();

  for (const [key, rawValue] of Object.entries(queryObject || {})) {
    if (Array.isArray(rawValue)) {
      rawValue.forEach((value) => params.append(key, String(value)));
    } else if (rawValue !== undefined && rawValue !== null) {
      params.append(key, String(rawValue));
    }
  }

  return params;
}

function escapeIgdbString(value) {
  return String(value).replace(/\\/g, "\\\\").replace(/\"/g, '\\\"');
}

async function callIgdbGames(query, clientId) {
  let accessToken = await getIgdbAccessToken(false);
  let response = await fetchIgdbGames(query, clientId, accessToken);

  if (response.status === 401) {
    accessToken = await getIgdbAccessToken(true);
    response = await fetchIgdbGames(query, clientId, accessToken);
  }

  return response;
}

async function fetchIgdbGames(query, clientId, accessToken) {
  return fetch("https://api.igdb.com/v4/games", {
    method: "POST",
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${accessToken}`,
      "Client-ID": clientId,
      "Content-Type": "text/plain"
    },
    body: query
  });
}

async function getIgdbAccessToken(forceRefresh) {
  if (!forceRefresh && igdbTokenState.accessToken && Date.now() < igdbTokenState.expiresAt - 60000) {
    return igdbTokenState.accessToken;
  }

  if (!forceRefresh && igdbTokenState.inFlight) {
    return igdbTokenState.inFlight;
  }

  igdbTokenState.inFlight = (async () => {
    const clientId = getRequiredEnv("IGDB_TWITCH_CLIENT_ID");
    const clientSecret = getRequiredEnv("IGDB_TWITCH_CLIENT_SECRET");
    const body = new URLSearchParams({
      client_id: clientId,
      client_secret: clientSecret,
      grant_type: "client_credentials"
    });

    const response = await fetch("https://id.twitch.tv/oauth2/token", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded"
      },
      body
    });

    const payload = await response.json();
    if (!response.ok || !payload.access_token) {
      const error = new Error(`Failed to acquire IGDB access token: ${response.status}`);
      error.statusCode = 502;
      error.details = payload;
      throw error;
    }

    igdbTokenState.accessToken = payload.access_token;
    igdbTokenState.expiresAt = Date.now() + (Number(payload.expires_in) || 0) * 1000;

    return igdbTokenState.accessToken;
  })();

  try {
    return await igdbTokenState.inFlight;
  } finally {
    igdbTokenState.inFlight = null;
  }
}

async function relayResponse(upstream, res) {
  const body = await upstream.text();
  copyHeader(upstream, res, "content-type");
  copyHeader(upstream, res, "cache-control");
  res.status(upstream.status).send(body);
  return body;
}

function copyHeader(upstream, res, headerName) {
  const value = upstream.headers.get(headerName);
  if (value) {
    res.set(headerName, value);
  }
}