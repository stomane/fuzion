const express = require("express");
const crypto = require("crypto");
const dns = require("dns").promises;
const path = require("path");
const { Storage } = require("@google-cloud/storage");
const { Pool } = require("pg");

const app = express();
const port = parseInt(process.env.PORT || "8080", 10);
const defaultGeminiModel = process.env.GEMINI_DEFAULT_MODEL || "gemini-3.5-flash-lite";
const customSearchCacheTtlSeconds = parseInt(process.env.CUSTOM_SEARCH_CACHE_TTL_SECONDS || "604800", 10);
const igdbCacheTtlSeconds = parseInt(process.env.IGDB_CACHE_TTL_SECONDS || "604800", 10);
const assetBucketName = getOptionalEnv("ASSET_BUCKET");
const iconFetchTimeoutMs = clampInteger(process.env.ICON_FETCH_TIMEOUT_MS, 1000, 60000, 10000);
const maxIconBytes = 8 * 1024 * 1024;
const maxConcurrentIconCaches = clampInteger(process.env.ICON_CACHE_CONCURRENCY, 1, 32, 4);
const publicBaseUrl = getOptionalEnv("PUBLIC_BASE_URL").replace(/\/+$/, "");
const rateLimitWindowMs = clampInteger(process.env.RATE_LIMIT_WINDOW_MS, 1000, 3600000, 60000);
// Off unless explicitly configured. Enabling it requires knowing how many entries the
// infrastructure in front of this service appends to X-Forwarded-For (TRUSTED_PROXY_HOPS):
// too few and a caller can forge a fresh bucket per request, too many and every user
// collapses into one shared bucket and throttles each other.
const rateLimitMaxRequests = clampInteger(process.env.RATE_LIMIT_MAX_REQUESTS, 0, 100000, 0);
const trustedProxyHops = clampInteger(process.env.TRUSTED_PROXY_HOPS, 1, 8, 1);
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
const storageClient = assetBucketName ? new Storage() : null;

app.disable("x-powered-by");
// Deliberately NOT `true`: that makes req.ip the leftmost X-Forwarded-For entry, which a
// client can set itself. Rate limiting keys off clientAddress() instead, which reads the
// rightmost entry - the one appended by the infrastructure in front of us.
app.set("trust proxy", false);
app.use(express.json({ limit: "256kb" }));
app.use(express.text({ type: ["text/plain", "application/apicalypse"], limit: "32kb" }));
// The desktop client pushes its whole library in a single form-urlencoded POST, so this
// ceiling scales with the largest library rather than the typical one. Measured: 2000 games
// is ~440kb encoded typically and ~1010kb with long titles and long icon URLs - i.e. the
// previous 1mb limit had no headroom left at that size and would 413 the whole push.
app.use(express.urlencoded({ extended: false, limit: "16mb" }));

void initializeDatabase();

app.get("/health", (req, res) => {
  res.json({
    ok: true,
    service: "fuzion-backend",
    project: process.env.GOOGLE_CLOUD_PROJECT || null,
    region: process.env.GOOGLE_CLOUD_REGION || null,
    database: dbPool ? "configured" : "disabled",
    assetBucket: assetBucketName || null
  });
});

// Only the endpoints that spend third-party quota are throttled. /get/* and /insert/* touch
// nothing but our own database and are legitimately bulk - a 2000-game library issues one
// lookup per title - so throttling those would break large libraries rather than protect us.
app.post("/gemini", rateLimit(), handleAsync(async (req, res) => {
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

app.get("/custom-search", rateLimit(), handleAsync(async (req, res) => {
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

app.all(["/production/v4/games", "/igdb/v4/games"], rateLimit(), handleAsync(async (req, res) => {
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
  const result = promoteCachedIcon(req, await getMetadataGame(gameName, falsePositive), "metadata_games");

  res.json({
    status: !!result,
    result: result ? [result] : []
  });
}));

app.get("/get/program", handleAsync(async (req, res) => {
  const programName = typeof req.query.programname === "string" ? req.query.programname : "";
  const falsePositive = parseBooleanQuery(req.query.falsepositive);
  const result = promoteCachedIcon(req, await getMetadataProgram(programName, falsePositive), "metadata_programs");

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

app.get("/asset/*", handleAsync(async (req, res) => {
  if (!storageClient || !assetBucketName) {
    res.status(404).json({ error: "Asset storage is not configured." });
    return;
  }

  const objectName = decodeURIComponent(req.params[0] || "");
  if (!objectName) {
    res.status(400).json({ error: "Missing asset path." });
    return;
  }

  const file = storageClient.bucket(assetBucketName).file(objectName);
  const [exists] = await file.exists();
  if (!exists) {
    res.status(404).json({ error: "Asset not found." });
    return;
  }

  const [metadata] = await file.getMetadata();
  if (metadata.contentType) {
    res.type(metadata.contentType);
  }
  res.set("Cache-Control", metadata.cacheControl || "public, max-age=86400");

  await streamFile(file, res);
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

// In-process fixed-window limiter. This is a damage cap, not a real defence: Cloud Run runs
// many instances and each keeps its own counters, so the effective global limit scales with
// instance count. Cloud Armor or API Gateway in front of the service is the actual fix.
const rateLimitBuckets = new Map();

function rateLimit() {
  return (req, res, next) => {
    if (rateLimitMaxRequests === 0) {
      next();
      return;
    }

    const now = Date.now();
    const key = clientAddress(req);
    const bucket = rateLimitBuckets.get(key);

    if (!bucket || now >= bucket.resetAt) {
      rateLimitBuckets.set(key, { count: 1, resetAt: now + rateLimitWindowMs });
      pruneRateLimitBuckets(now);
      next();
      return;
    }

    bucket.count += 1;

    if (bucket.count > rateLimitMaxRequests) {
      res.set("Retry-After", String(Math.max(1, Math.ceil((bucket.resetAt - now) / 1000))));
      res.status(429).json({ error: "Too many requests." });
      return;
    }

    next();
  };
}

function pruneRateLimitBuckets(now) {
  if (rateLimitBuckets.size < 10000) {
    return;
  }

  for (const [key, bucket] of rateLimitBuckets) {
    if (now >= bucket.resetAt) {
      rateLimitBuckets.delete(key);
    }
  }
}

// Everything a client sends in X-Forwarded-For is attacker-controlled; only the entries the
// infrastructure appends are trustworthy. TRUSTED_PROXY_HOPS says how many that is, so we
// index that many places from the right. Verify it against the real deployment before
// enabling rate limiting - see the note on rateLimitMaxRequests.
function clientAddress(req) {
  const forwardedFor = String(req.headers["x-forwarded-for"] || "")
    .split(",")
    .map((part) => part.trim())
    .filter(Boolean);

  const index = forwardedFor.length - trustedProxyHops;
  if (index >= 0 && index < forwardedFor.length) {
    return forwardedFor[index];
  }

  return req.socket && req.socket.remoteAddress ? req.socket.remoteAddress : "unknown";
}

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
          cached_icon_path text,
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
          cached_icon_path text,
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

        alter table metadata_games add column if not exists cached_icon_path text;
        alter table metadata_programs add column if not exists cached_icon_path text;
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
        select id, gamename, iconlink, cached_icon_path, exename, falsepositive, iconrelevance
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
      cached_icon_path: row.cached_icon_path || "",
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
        select id, name, iconlink, cached_icon_path, exename, falsepositive
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
      cached_icon_path: row.cached_icon_path || "",
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
              -- >= for iconlink but > for iconrelevance is deliberate, carried over from the
              -- legacy MySQL upsert in FuzionDock/SQL/SQLManager.cs: the client always pushes
              -- relevance 10, so >= is what lets a re-push refresh a stale or broken icon.
              iconlink = case
                when excluded.iconrelevance >= metadata_games.iconrelevance and excluded.iconlink is not null then excluded.iconlink
                else metadata_games.iconlink
              end,
              -- the cached object is keyed by the source URL, so a replaced iconlink must
              -- drop the old cached_icon_path or /get/main keeps serving the previous image
              cached_icon_path = case
                when excluded.iconrelevance >= metadata_games.iconrelevance
                  and excluded.iconlink is not null
                  and excluded.iconlink is distinct from metadata_games.iconlink then null
                else metadata_games.cached_icon_path
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
              -- see upsertMetadataGames: a replaced iconlink invalidates the cached object
              cached_icon_path = case
                when excluded.iconlink is not null
                  and excluded.iconlink is distinct from metadata_programs.iconlink then null
                else metadata_programs.cached_icon_path
              end,
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
    return enforceIgdbLimit(req.query.query.trim());
  }

  if (typeof req.body === "string" && req.body.trim()) {
    return enforceIgdbLimit(req.body.trim());
  }

  if (req.body && typeof req.body === "object" && typeof req.body.query === "string" && req.body.query.trim()) {
    return enforceIgdbLimit(req.body.query.trim());
  }

  return buildIgdbQuery(toSearchParams(req.query));
}

// Raw Apicalypse from the caller is passed straight through to IGDB, so the ceiling that
// buildIgdbQuery applies has to be re-applied here or it is trivially bypassed with
// "limit 500;" - IGDB quota is billed to our client id, not the caller's.
function enforceIgdbLimit(query) {
  const requested = query.match(/\blimit\s+(\d+)\s*;/i);
  const limit = clampInteger(requested ? requested[1] : null, 1, 50, 5);
  const withoutLimit = query.replace(/\blimit\s+\d+\s*;/gi, "").trim();

  return `${withoutLimit}${withoutLimit.endsWith(";") || withoutLimit === "" ? "" : ";"} limit ${limit};`.trim();
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

async function streamFile(file, res) {
  await new Promise((resolve, reject) => {
    file.createReadStream()
      .on("error", reject)
      .on("end", resolve)
      .pipe(res);
  });
}

function buildAssetUrl(req, objectName) {
  const encodedPath = objectName.split("/").map(encodeURIComponent).join("/");
  return `${resolvePublicBaseUrl(req)}/asset/${encodedPath}`;
}

// req.protocol is not usable here: trust proxy is off (see above), so it always reports
// "http" behind Cloud Run's TLS termination and would hand clients http:// icon URLs.
function resolvePublicBaseUrl(req) {
  if (publicBaseUrl) {
    return publicBaseUrl;
  }

  const forwardedProto = String(req.get("x-forwarded-proto") || "").split(",")[0].trim();
  const protocol = forwardedProto === "http" || forwardedProto === "https" ? forwardedProto : "https";

  return `${protocol}://${req.get("host")}`;
}

function buildAssetObjectName(sourceUrl, contentType) {
  const urlHash = crypto.createHash("sha256").update(sourceUrl).digest("hex");
  const extension = getExtensionForContentType(contentType, sourceUrl);
  return `icons/${urlHash}${extension}`;
}

function getExtensionForContentType(contentType, sourceUrl) {
  const normalizedType = String(contentType || "").toLowerCase();
  if (normalizedType.includes("png")) {
    return ".png";
  }
  if (normalizedType.includes("jpeg") || normalizedType.includes("jpg")) {
    return ".jpg";
  }
  if (normalizedType.includes("webp")) {
    return ".webp";
  }
  if (normalizedType.includes("gif")) {
    return ".gif";
  }

  const parsedPath = path.extname(new URL(sourceUrl).pathname || "").toLowerCase();
  return parsedPath && parsedPath.length <= 5 ? parsedPath : ".img";
}

async function isPubliclyRoutableHost(hostname) {
  if (!hostname) {
    return false;
  }

  try {
    const records = await dns.lookup(hostname, { all: true });
    return records.length > 0 && records.every((record) => isPublicAddress(record.address, record.family));
  } catch (error) {
    return false;
  }
}

function isPublicAddress(address, family) {
  const normalized = String(address).toLowerCase();

  if (family === 6) {
    const mapped = normalized.match(/^::ffff:(\d+\.\d+\.\d+\.\d+)$/);
    if (mapped) {
      return isPublicAddress(mapped[1], 4);
    }

    if (normalized === "::" || normalized === "::1") {
      return false;
    }

    return !/^f[cd]/.test(normalized) && !/^fe[89ab]/.test(normalized);
  }

  const octets = normalized.split(".").map(Number);
  if (octets.length !== 4 || octets.some((octet) => !Number.isInteger(octet) || octet < 0 || octet > 255)) {
    return false;
  }

  const [first, second] = octets;
  if (first === 0 || first === 10 || first === 127) {
    return false;
  }
  if (first === 169 && second === 254) {
    return false;
  }
  if (first === 172 && second >= 16 && second <= 31) {
    return false;
  }
  if (first === 192 && (second === 0 || second === 168)) {
    return false;
  }
  if (first === 100 && second >= 64 && second <= 127) {
    return false;
  }

  return first < 224;
}

async function ensureCachedIconAsset(sourceUrl) {
  if (!storageClient || !assetBucketName || !sourceUrl) {
    return "";
  }

  let parsed;
  try {
    parsed = new URL(sourceUrl);
  } catch (error) {
    return "";
  }

  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
    return "";
  }

  // iconlink values arrive from unauthenticated /insert/* callers, so this fetch is
  // attacker-directed: refuse anything that resolves off the public internet.
  if (!(await isPubliclyRoutableHost(parsed.hostname))) {
    return "";
  }

  const response = await fetch(sourceUrl, {
    redirect: "error",
    signal: AbortSignal.timeout(iconFetchTimeoutMs),
    headers: { Accept: "image/*" }
  });
  if (!response.ok) {
    return "";
  }

  const contentType = String(response.headers.get("content-type") || "").toLowerCase();
  if (!contentType.startsWith("image/")) {
    return "";
  }

  const contentLength = Number(response.headers.get("content-length") || 0);
  if (contentLength > maxIconBytes) {
    return "";
  }

  const objectName = buildAssetObjectName(sourceUrl, contentType);
  const bucket = storageClient.bucket(assetBucketName);
  const file = bucket.file(objectName);
  const [exists] = await file.exists();
  if (exists) {
    return objectName;
  }

  const bytes = Buffer.from(await response.arrayBuffer());
  if (bytes.length > maxIconBytes) {
    return "";
  }

  await file.save(bytes, {
    resumable: false,
    metadata: {
      contentType,
      cacheControl: "public, max-age=31536000, immutable",
      metadata: {
        sourceUrl
      }
    }
  });

  return objectName;
}

// Caching an icon is best-effort: a dead or slow source URL must never take down the
// metadata lookup that happens to be carrying it.
async function tryEnsureCachedIconAsset(sourceUrl) {
  try {
    return await ensureCachedIconAsset(sourceUrl);
  } catch (error) {
    console.error(`Icon cache failed for ${sourceUrl}:`, error && error.message ? error.message : error);
    return "";
  }
}

// Caching runs in the background rather than inline. A client scanning a large library
// issues one blocking lookup per title, so downloading the icon inside the handler would
// add a full round trip to every first-time title - 2000 of them for a big library.
// The first lookup returns the original iconlink and schedules the copy; later lookups
// return the cached asset URL.
const inFlightIconCaches = new Set();
let activeIconCaches = 0;

function scheduleIconCache(sourceUrl, table, id) {
  if (!storageClient || !assetBucketName || !sourceUrl || !id) {
    return;
  }

  if (inFlightIconCaches.has(sourceUrl)) {
    return;
  }

  // Bounded, and drops work instead of queueing it: each icon can hold maxIconBytes in
  // memory, and an unbounded queue over a 2000-title scan would exhaust the instance.
  // Anything dropped is simply rescheduled by the next lookup for that title.
  if (activeIconCaches >= maxConcurrentIconCaches) {
    return;
  }

  inFlightIconCaches.add(sourceUrl);
  activeIconCaches += 1;

  void (async () => {
    try {
      const objectName = await tryEnsureCachedIconAsset(sourceUrl);
      if (!objectName) {
        return;
      }

      await withDatabase(
        (client) => client.query(
          `update ${table} set cached_icon_path = $1, updated_at = now()
           where id = $2 and cached_icon_path is null`,
          [objectName, id]
        ),
        false
      );
    } finally {
      activeIconCaches -= 1;
      inFlightIconCaches.delete(sourceUrl);
    }
  })();
}

function promoteCachedIcon(req, result, table) {
  if (!result) {
    return result;
  }

  if (result.cached_icon_path) {
    result.iconlink = buildAssetUrl(req, result.cached_icon_path);
  } else if (result.iconlink) {
    scheduleIconCache(result.iconlink, table, result.id);
  }

  delete result.cached_icon_path;
  return result;
}

function copyHeader(upstream, res, headerName) {
  const value = upstream.headers.get(headerName);
  if (value) {
    res.set(headerName, value);
  }
}