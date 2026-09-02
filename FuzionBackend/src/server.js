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
// Whether a program is a game barely changes, so this is cached far longer than the search
// caches - the whole point is that a rescan never reaches Gemini again.
const classificationCacheTtlSeconds = clampInteger(process.env.CLASSIFICATION_CACHE_TTL_SECONDS, 1, 31536000, 2592000);
const classificationBatchSize = clampInteger(process.env.CLASSIFICATION_BATCH_SIZE, 1, 200, 75);
const maxClassificationItems = clampInteger(process.env.CLASSIFICATION_MAX_ITEMS, 1, 5000, 500);
// Wall-clock ceiling for the IGDB stage of a single classify request. IGDB is queried one
// title at a time and rate-limits hard, so it gets a slice of the request rather than the
// whole thing; whatever it does not reach in time goes to Gemini.
const igdbResolveBudgetMs = clampInteger(process.env.IGDB_RESOLVE_BUDGET_MS, 0, 120000, 15000);
const assetBucketName = getOptionalEnv("ASSET_BUCKET");
const iconFetchTimeoutMs = clampInteger(process.env.ICON_FETCH_TIMEOUT_MS, 1000, 60000, 10000);
const maxIconBytes = 8 * 1024 * 1024;
const maxConcurrentIconCaches = clampInteger(process.env.ICON_CACHE_CONCURRENCY, 1, 32, 4);
// /insert/* is unauthenticated, so a client-supplied relevance is not evidence of anything.
// It used to be trusted, which let one caller claim relevance 100 and permanently lock an
// icon that no legitimate push (always 10) could ever replace.
const desktopPushIconRelevance = 10;
const iconConfirmationsRequired = clampInteger(process.env.ICON_CONFIRMATIONS_REQUIRED, 1, 100, 3);
const iconProposalRetentionDays = clampInteger(process.env.ICON_PROPOSAL_RETENTION_DAYS, 1, 3650, 30);
const submitterSalt = getOptionalEnv("SUBMITTER_SALT");
// Table names are interpolated into the icon-promotion statements, so they are checked
// against this set rather than taken on trust from the caller.
const metadataTables = new Set(["metadata_games", "metadata_programs"]);
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
// Opt-in access log. Off by default so it costs nothing in production, but invaluable
// when tracing what the desktop client actually asked for and in what order.
if (getOptionalEnv("REQUEST_LOG")) {
  app.use((req, res, next) => {
    const startedAt = Date.now();
    res.on("finish", () => {
      console.log("[req] " + req.method + " " + req.originalUrl.slice(0, 140) +
        " -> " + res.statusCode + " " + (Date.now() - startedAt) + "ms");
    });
    next();
  });
}

app.use(express.json({ limit: "256kb" }));
app.use(express.text({ type: ["text/plain", "application/apicalypse"], limit: "32kb" }));
// The desktop client pushes its whole library in a single form-urlencoded POST, so this
// ceiling scales with the largest library rather than the typical one. Measured: 2000 games
// is ~440kb encoded typically and ~1010kb with long titles and long icon URLs - i.e. the
// previous 1mb limit had no headroom left at that size and would 413 the whole push.
app.use(express.urlencoded({ extended: false, limit: "16mb" }));

void initializeDatabase();

if (dbPool) {
  // unref so a pending timer never keeps the instance alive on shutdown
  setInterval(() => void pruneIconProposals().catch(() => {}), 6 * 60 * 60 * 1000).unref();
}

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
  const inserted = await upsertMetadataGames(records, submitterFingerprint(req));

  res.json({
    ok: true,
    inserted
  });
}));

app.post("/insert/program", handleAsync(async (req, res) => {
  const records = parsePostedJsonArray(req.body && req.body.data, "program list");
  const inserted = await upsertMetadataPrograms(records, submitterFingerprint(req));

  res.json({
    ok: true,
    inserted
  });
}));

// Classifies programs as games, answering from the cache wherever possible and asking
// Gemini only about the remainder. Items with no verdict - a batch that failed, or Gemini
// being unreachable - come back in `unresolved` rather than as a negative, so the caller can
// fall back to its other checks instead of recording "not a game" on our say-so.
app.post("/classify/programs", rateLimit(), handleAsync(async (req, res) => {
  const items = normalizeClassificationItems(req.body && req.body.items);

  if (items.length === 0) {
    res.json({
      ok: true,
      results: [],
      unresolved: [],
      stats: { total: 0, fromCache: 0, fromIgdb: 0, fromGemini: 0, unresolved: 0 }
    });
    return;
  }

  const model = resolveGeminiModel(typeof req.query.model === "string" ? req.query.model : "");

  // Stage 1: our own cache answers whatever it can, for free.
  const cached = await getCachedVerdicts(items);
  const misses = items.filter((item) => !cached.has(item.cacheKey));

  // Stage 2: IGDB confirms games it recognises.
  const igdb = await resolveWithIgdb(misses);

  // Stage 3: Gemini judges everything still unaccounted for, in batches.
  const gemini = await classifyWithGemini(igdb.remaining, model);

  const fresh = new Map([...igdb.verdicts, ...gemini.verdicts]);
  await storeVerdicts(fresh, model);

  const results = [];
  for (const item of items) {
    const verdict = cached.get(item.cacheKey) || fresh.get(item.cacheKey);
    if (verdict) {
      results.push({
        detectedName: item.detectedName,
        isGame: verdict.isGame,
        canonicalTitle: verdict.isGame ? verdict.canonicalTitle || item.detectedName : null
      });
    }
  }

  res.json({
    ok: true,
    results,
    unresolved: gemini.unresolved.map((item) => item.detectedName),
    stats: {
      total: items.length,
      fromCache: cached.size,
      fromIgdb: igdb.verdicts.size,
      fromGemini: gemini.verdicts.size,
      unresolved: gemini.unresolved.length
    }
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

// Identifies the source of a submission for confirmation counting. Stored as a keyed hash
// rather than an address: this is abuse accounting, and there is no reason to keep a table
// of who looked up which games.
function submitterFingerprint(req) {
  return crypto
    .createHmac("sha256", submitterSalt)
    .update(identityScopeForAddress(clientAddress(req)))
    .digest("hex")
    .slice(0, 32);
}

// IPv6 subscribers are normally handed a whole /64 and rotate the host half (privacy
// extensions), so fingerprinting a full v6 address would let one machine supply an endless
// stream of distinct "submitters" and confirm its own proposals. Collapse to the /64.
function identityScopeForAddress(address) {
  const cleaned = String(address).replace(/^\[|\]$/g, "").split("%")[0];

  if (!cleaned.includes(":")) {
    return cleaned;
  }

  const mapped = cleaned.match(/^::ffff:(\d+\.\d+\.\d+\.\d+)$/i);
  if (mapped) {
    return mapped[1];
  }

  return expandIpv6(cleaned).slice(0, 4).join(":");
}

function expandIpv6(address) {
  const [head, tail] = address.split("::");
  const headGroups = head ? head.split(":").filter(Boolean) : [];
  const tailGroups = tail !== undefined && tail ? tail.split(":").filter(Boolean) : [];
  const missing = 8 - headGroups.length - tailGroups.length;
  const filler = address.includes("::") ? new Array(Math.max(0, missing)).fill("0") : [];

  return [...headGroups, ...filler, ...tailGroups]
    .slice(0, 8)
    .map((group) => group.toLowerCase().padStart(4, "0"));
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

  const host = getOptionalEnv("POSTGRES_HOST");

  if (!database || !user || !password || (!connectionName && !host)) {
    return null;
  }

  return new Pool({
    // Cloud SQL connects over its unix socket; POSTGRES_HOST is the escape hatch for
    // running against a plain Postgres locally.
    ...(connectionName
      ? { host: `/cloudsql/${connectionName}` }
      : { host, port: clampInteger(process.env.POSTGRES_PORT, 1, 65535, 5432) }),
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

        -- One row per program we have an is-game verdict for, whatever produced it, keyed by
        -- the identifying fields rather than by prompt text: batches are composed per user,
        -- so a prompt-level cache would miss as soon as anyone installs one new program.
        -- IGDB verdicts live here alongside Gemini's, so a second run reaches neither.
        -- Negatives are stored too: most entries in a library are not games, and re-asking
        -- about those is the bulk of the spend.
        create table if not exists program_verdict_cache (
          cache_key text primary key,
          detected_name text not null,
          publisher text,
          launcher text,
          exe_name text,
          is_game boolean not null,
          canonical_title text,
          source text not null,
          model text,
          evidence jsonb,
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

        -- Pending icon changes to rows that already have an icon. /insert/* is public, so a
        -- replacement is treated as a proposal and only applied once enough distinct
        -- submitters independently send the same one.
        create table if not exists metadata_icon_proposals (
          entity_type text not null,
          normalized_name text not null,
          iconlink text not null,
          submitter_hash text not null,
          created_at timestamptz not null default now(),
          primary key (entity_type, normalized_name, iconlink, submitter_hash)
        );

        create index if not exists idx_metadata_games_falsepositive on metadata_games(falsepositive);
        create index if not exists idx_metadata_programs_falsepositive on metadata_programs(falsepositive);
        create index if not exists idx_custom_search_cache_expires_at on custom_search_cache(expires_at);
        create index if not exists idx_igdb_search_cache_expires_at on igdb_search_cache(expires_at);
        create index if not exists idx_icon_proposals_created_at on metadata_icon_proposals(created_at);
        create index if not exists idx_program_verdict_expires_at on program_verdict_cache(expires_at);

        alter table metadata_games add column if not exists cached_icon_path text;
        alter table metadata_programs add column if not exists cached_icon_path text;

        -- Who contributed the icon a row currently carries. Null means nobody owns it:
        -- either it predates this column or it was promoted by consensus, and in both
        -- cases every future change needs confirmation.
        alter table metadata_games add column if not exists icon_submitter_hash text;
        alter table metadata_programs add column if not exists icon_submitter_hash text;
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

// Proposals that never reach the confirmation threshold would otherwise accumulate for
// every icon anyone ever disagreed about.
async function pruneIconProposals() {
  await withDatabase(
    (client) => client.query(
      `delete from metadata_icon_proposals where created_at < now() - ($1 || ' days')::interval`,
      [String(iconProposalRetentionDays)]
    ),
    null
  );
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

async function upsertMetadataGames(records, submitter) {
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

      // An icon is adopted here only when the row has none. Replacing one that is already
      // established goes through reconcileIconSubmission, so a single unauthenticated
      // caller cannot change what every user sees.
      const stored = await client.query(
        `
          insert into metadata_games
            (normalized_name, gamename, canonical_name, iconlink, icon_submitter_hash, exename, falsepositive, iconrelevance, source, updated_at)
          values ($1, $2, $2, nullif($3, ''), case when nullif($3, '') is null then null else $5 end, nullif($4, ''), false, $6, 'desktop-push', now())
          on conflict (normalized_name) do update
          set gamename = excluded.gamename,
              canonical_name = coalesce(metadata_games.canonical_name, excluded.canonical_name),
              iconlink = coalesce(metadata_games.iconlink, excluded.iconlink),
              icon_submitter_hash = case
                when metadata_games.iconlink is null then excluded.icon_submitter_hash
                else metadata_games.icon_submitter_hash
              end,
              exename = coalesce(excluded.exename, metadata_games.exename),
              iconrelevance = greatest(metadata_games.iconrelevance, excluded.iconrelevance),
              source = 'desktop-push',
              updated_at = now()
          returning iconlink, icon_submitter_hash
        `,
        [normalized, gameName, iconLink || null, exeName || null, submitter, desktopPushIconRelevance]
      );

      await reconcileIconSubmission(client, "metadata_games", "game", normalized, iconLink, submitter, stored.rows[0]);
      inserted += 1;
    }

    return inserted;
  }, 0);
}

// Applies an icon change to a row that already has one. The contributor who established the
// current icon may replace it outright - that keeps the ordinary "my icon got better" path
// working for titles only one person has. Everyone else is casting a vote, and the change
// lands only once ICON_CONFIRMATIONS_REQUIRED distinct submitters send the same URL.
async function reconcileIconSubmission(client, table, entityType, normalizedName, iconLink, submitter, stored) {
  if (!iconLink || !stored || !stored.iconlink || stored.iconlink === iconLink) {
    return;
  }

  if (!metadataTables.has(table)) {
    throw new Error(`Refusing to update unknown table: ${table}`);
  }

  if (stored.icon_submitter_hash && stored.icon_submitter_hash === submitter) {
    await client.query(
      `update ${table} set iconlink = $1, cached_icon_path = null, updated_at = now()
       where normalized_name = $2`,
      [iconLink, normalizedName]
    );
    return;
  }

  await client.query(
    `insert into metadata_icon_proposals (entity_type, normalized_name, iconlink, submitter_hash)
     values ($1, $2, $3, $4)
     on conflict do nothing`,
    [entityType, normalizedName, iconLink, submitter]
  );

  const tally = await client.query(
    `select count(distinct submitter_hash)::int as confirmations
     from metadata_icon_proposals
     where entity_type = $1 and normalized_name = $2 and iconlink = $3`,
    [entityType, normalizedName, iconLink]
  );

  if ((tally.rows[0] && tally.rows[0].confirmations) < iconConfirmationsRequired) {
    return;
  }

  // Promoted icons are left unowned, so the next change needs fresh confirmation rather
  // than inheriting the last voter's ability to overwrite at will.
  await client.query(
    `update ${table} set iconlink = $1, cached_icon_path = null, icon_submitter_hash = null, updated_at = now()
     where normalized_name = $2`,
    [iconLink, normalizedName]
  );

  await client.query(
    `delete from metadata_icon_proposals where entity_type = $1 and normalized_name = $2`,
    [entityType, normalizedName]
  );
}

async function upsertMetadataPrograms(records, submitter) {
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

      // Same rule as upsertMetadataGames: adopt an icon only when there is none.
      const stored = await client.query(
        `
          insert into metadata_programs
            (normalized_name, name, iconlink, icon_submitter_hash, exename, falsepositive, updated_at)
          values ($1, $2, nullif($3, ''), case when nullif($3, '') is null then null else $5 end, nullif($4, ''), false, now())
          on conflict (normalized_name) do update
          set name = excluded.name,
              iconlink = coalesce(metadata_programs.iconlink, excluded.iconlink),
              icon_submitter_hash = case
                when metadata_programs.iconlink is null then excluded.icon_submitter_hash
                else metadata_programs.icon_submitter_hash
              end,
              exename = coalesce(excluded.exename, metadata_programs.exename),
              updated_at = now()
          returning iconlink, icon_submitter_hash
        `,
        [normalized, name, iconLink || null, exeName || null, submitter]
      );

      await reconcileIconSubmission(client, "metadata_programs", "program", normalized, iconLink, submitter, stored.rows[0]);
      inserted += 1;
    }

    return inserted;
  }, 0);
}

function normalizeClassificationItems(rawItems) {
  if (!Array.isArray(rawItems)) {
    return [];
  }

  const seen = new Set();
  const items = [];

  for (const raw of rawItems.slice(0, maxClassificationItems)) {
    if (!raw || typeof raw !== "object") {
      continue;
    }

    const detectedName = String(raw.detectedName || raw.detectedname || "").trim();
    if (!detectedName) {
      continue;
    }

    const item = {
      detectedName,
      publisher: String(raw.publisher || "").trim() || "unknown",
      launcher: String(raw.launcher || "").trim() || "unknown",
      exeName: String(raw.exeName || raw.exename || "").trim() || "unknown"
    };
    item.cacheKey = buildClassificationKey(item);

    // A library can list the same program more than once; asking about it twice in one
    // batch would spend tokens for an answer we already have in hand.
    if (seen.has(item.cacheKey)) {
      continue;
    }

    seen.add(item.cacheKey);
    items.push(item);
  }

  return items;
}

function buildClassificationKey(item) {
  return crypto
    .createHash("sha256")
    .update([item.detectedName, item.publisher, item.launcher, item.exeName].map(normalizeName).join(" "))
    .digest("hex");
}

// Verdicts are keyed on the program's identifying fields, not on the model, so an IGDB
// verdict and a Gemini one are interchangeable here: whichever answered first spares the
// next run from asking either service again.
async function getCachedVerdicts(items) {
  const found = new Map();

  await withDatabase(async (client) => {
    const result = await client.query(
      `select cache_key, is_game, canonical_title, source
       from program_verdict_cache
       where expires_at > now() and cache_key = any($1::text[])`,
      [items.map((item) => item.cacheKey)]
    );

    for (const row of result.rows) {
      found.set(row.cache_key, {
        isGame: row.is_game,
        canonicalTitle: row.canonical_title || "",
        source: row.source
      });
    }

    return true;
  }, false);

  return found;
}

async function storeVerdicts(verdicts, model) {
  if (verdicts.size === 0) {
    return;
  }

  const keys = [];
  const names = [];
  const publishers = [];
  const launchers = [];
  const exeNames = [];
  const isGames = [];
  const titles = [];
  const sources = [];
  const evidence = [];

  for (const [cacheKey, verdict] of verdicts) {
    keys.push(cacheKey);
    names.push(verdict.detectedName);
    // The identifying fields are stored alongside the verdict, not just folded into the
    // hash: two programs can share a name and get opposite verdicts, and the row has to say
    // which one it is. "Parsec" by Parsec Cloud is a different row from a game of that name.
    publishers.push(verdict.publisher || null);
    launchers.push(verdict.launcher || null);
    exeNames.push(verdict.exeName || null);
    isGames.push(verdict.isGame);
    titles.push(verdict.canonicalTitle || null);
    sources.push(verdict.source);
    evidence.push(verdict.evidence ? JSON.stringify(verdict.evidence) : null);
  }

  await withDatabase(
    (client) => client.query(
      `insert into program_verdict_cache
         (cache_key, detected_name, publisher, launcher, exe_name, is_game, canonical_title,
          source, model, evidence, expires_at, updated_at)
       select key, name, pub, launcher, exe, is_game, nullif(title, ''), src,
              case when src = 'gemini' then $10::text else null end,
              ev::jsonb,
              now() + ($11 || ' seconds')::interval, now()
       from unnest($1::text[], $2::text[], $3::text[], $4::text[], $5::text[],
                   $6::boolean[], $7::text[], $8::text[], $9::text[])
         as t(key, name, pub, launcher, exe, is_game, title, src, ev)
       on conflict (cache_key) do update
       set is_game = excluded.is_game,
           canonical_title = excluded.canonical_title,
           detected_name = excluded.detected_name,
           publisher = excluded.publisher,
           launcher = excluded.launcher,
           exe_name = excluded.exe_name,
           source = excluded.source,
           model = excluded.model,
           evidence = excluded.evidence,
           expires_at = excluded.expires_at,
           updated_at = now()`,
      [keys, names, publishers, launchers, exeNames, isGames, titles, sources, evidence,
       model, String(classificationCacheTtlSeconds)]
    ),
    null
  );
}

// IGDB stage. A match is positive evidence; no match is not evidence of "not a game", so
// unmatched items fall through to Gemini rather than being recorded as negatives. IGDB
// rate-limits hard, so this runs under a wall-clock budget and abandons the whole stage on
// the first 429 - the remainder simply becomes Gemini's problem, which is cheaper per item
// anyway since Gemini takes them 75 at a time.
async function resolveWithIgdb(items) {
  const verdicts = new Map();

  if (items.length === 0 || !process.env.IGDB_TWITCH_CLIENT_ID || !process.env.IGDB_TWITCH_CLIENT_SECRET) {
    return { verdicts, remaining: items };
  }

  const deadline = Date.now() + igdbResolveBudgetMs;
  const remaining = [];
  let abandoned = false;

  for (const item of items) {
    if (abandoned || Date.now() >= deadline) {
      remaining.push(item);
      continue;
    }

    try {
      const candidate = await findIgdbCandidate(item.detectedName);

      if (candidate && companyCorroborates(item.publisher, candidate.companies)) {
        verdicts.set(item.cacheKey, {
          detectedName: item.detectedName,
          publisher: item.publisher,
          launcher: item.launcher,
          exeName: item.exeName,
          isGame: true,
          canonicalTitle: candidate.name,
          source: "igdb",
          evidence: {
            igdbName: candidate.name,
            year: candidate.year || null,
            companies: candidate.companies.map((c) => c.name),
            matchedPublisher: item.publisher
          }
        });
        continue;
      }

      // A name match nobody corroborated is not a verdict - "Parsec" is both a 1982 TI-99
      // game and a remote-desktop tool - so it goes to Gemini like any other unresolved item.
      remaining.push(item);
    } catch (error) {
      if (error && error.rateLimited) {
        abandoned = true;
      }
      remaining.push(item);
    }
  }

  return { verdicts, remaining };
}

async function findIgdbCandidate(name) {
  const query = `fields name,first_release_date,involved_companies.company.name,involved_companies.publisher,involved_companies.developer; search "${escapeIgdbString(name)}"; limit 10;`;
  const cacheKey = hashCacheKey("igdb-games", query);
  const cached = await getCachedJson("igdb_search_cache", cacheKey);

  if (cached) {
    return bestIgdbMatch(name, cached);
  }

  const clientId = getRequiredEnv("IGDB_TWITCH_CLIENT_ID");
  const upstream = await callIgdbGames(query, clientId);
  const payloadText = await upstream.text();

  if (upstream.status === 429) {
    const error = new Error("IGDB rate limited");
    error.rateLimited = true;
    throw error;
  }

  if (!upstream.ok) {
    throw new Error(`IGDB search failed: ${upstream.status}`);
  }

  await upsertCachedJson("igdb_search_cache", cacheKey, query, payloadText, igdbCacheTtlSeconds);

  return bestIgdbMatch(name, JSON.parse(payloadText));
}

// IGDB search is fuzzy and will answer almost anything, so only a title that actually
// resembles the query counts. Exact match wins; otherwise the IGDB title has to be the
// whole of the detected name once decoration like a trademark symbol is stripped. The
// looser arm is only safe because a match still has to be corroborated by the publisher.
function bestIgdbMatch(name, payload) {
  if (!Array.isArray(payload)) {
    return null;
  }

  const wanted = normalizeTitle(name);
  let fallback = null;

  for (const entry of payload) {
    const candidateName = entry && typeof entry.name === "string" ? entry.name : "";
    if (!candidateName) {
      continue;
    }

    const normalized = normalizeTitle(candidateName);
    const candidate = {
      name: candidateName,
      year: entry.first_release_date ? new Date(entry.first_release_date * 1000).getUTCFullYear() : null,
      companies: extractIgdbCompanies(entry)
    };

    if (normalized === wanted) {
      // Prefer an exact match that actually carries company data, since a match with no
      // companies can never be corroborated.
      if (candidate.companies.length > 0) {
        return candidate;
      }
      fallback = fallback || candidate;
    }
  }

  return fallback;
}

function extractIgdbCompanies(entry) {
  const involved = Array.isArray(entry.involved_companies) ? entry.involved_companies : [];

  return involved
    .map((row) => ({
      name: row && row.company && typeof row.company.name === "string" ? row.company.name : "",
      publisher: !!(row && row.publisher),
      developer: !!(row && row.developer)
    }))
    .filter((company) => company.name);
}

// Installed metadata names the developer about as often as the publisher (Graveyard Keeper
// reports "Lazy Bear Games", which IGDB records as developer with tinyBuild publishing), so
// both roles count.
function companyCorroborates(installedPublisher, companies) {
  const installed = normalizeCompanyName(installedPublisher);
  if (!installed || !Array.isArray(companies) || companies.length === 0) {
    return false;
  }

  const installedTokens = new Set(installed.split(" ").filter(Boolean));
  if (installedTokens.size === 0) {
    return false;
  }

  for (const company of companies) {
    const candidateTokens = new Set(normalizeCompanyName(company.name).split(" ").filter(Boolean));
    if (candidateTokens.size === 0) {
      continue;
    }

    if (isTokenSubset(installedTokens, candidateTokens) || isTokenSubset(candidateTokens, installedTokens)) {
      return true;
    }
  }

  return false;
}

function isTokenSubset(inner, outer) {
  for (const token of inner) {
    if (!outer.has(token)) {
      return false;
    }
  }

  return true;
}

// Drops legal-entity noise so "Keen Games GmbH" and "Keen Games" compare equal.
const companySuffixes = new Set([
  "inc", "incorporated", "llc", "ltd", "limited", "gmbh", "co", "corp", "corporation",
  "sa", "ab", "oy", "as", "bv", "nv", "plc", "kk", "srl", "spa", "pty", "pte", "sarl"
]);

function normalizeCompanyName(value) {
  return String(value || "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .split(" ")
    .filter((token) => token && !companySuffixes.has(token))
    .join(" ")
    .trim();
}

// Titles carry decoration that never appears in IGDB: trademark marks, edition suffixes,
// punctuation. Normalised separately from company names, which need different handling.
function normalizeTitle(value) {
  return String(value || "")
    .toLowerCase()
    .replace(/[™®©]/g, " ")
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .replace(/s+/g, " ");
}

// Asks Gemini about the items that missed cache, in batches. A batch that fails leaves its
// items unresolved rather than failing the whole request: the caller still gets every
// verdict the other batches produced.
async function classifyWithGemini(items, model) {
  const verdicts = new Map();
  const unresolved = [];

  if (items.length === 0) {
    return { verdicts, unresolved };
  }

  for (let offset = 0; offset < items.length; offset += classificationBatchSize) {
    const batch = items.slice(offset, offset + classificationBatchSize);

    try {
      const named = await requestGeminiClassification(batch, model);

      for (const item of batch) {
        const match = named.get(item.detectedName.toLowerCase());
        // A successful batch that omits an item is Gemini saying "not a game" - the prompt
        // asks it to exclude anything it is unsure of. Recorded as a negative so the next
        // scan does not pay to ask again.
        verdicts.set(item.cacheKey, {
          detectedName: item.detectedName,
          publisher: item.publisher,
          launcher: item.launcher,
          exeName: item.exeName,
          isGame: !!match,
          canonicalTitle: match || "",
          source: "gemini"
        });
      }
    } catch (error) {
      console.error("Gemini classification batch failed:", error && error.message ? error.message : error);
      unresolved.push(...batch);
    }
  }

  return { verdicts, unresolved };
}

async function requestGeminiClassification(batch, model) {
  const apiKey = getRequiredEnv("GEMINI_API_KEY");
  const upstream = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(buildClassificationRequest(batch))
    }
  );

  if (!upstream.ok) {
    const detail = await upstream.text();
    throw new Error(`Gemini request failed: ${upstream.status} ${detail.slice(0, 200)}`);
  }

  return parseClassificationResponse(batch, await upstream.json());
}

function buildClassificationRequest(batch) {
  const lines = ["Return JSON only.",
    "For each input item that is an actual video game, include the exact detectedName from input and a cleaned canonicalTitle.",
    "If uncertain, exclude the item.",
    "Items:"];

  batch.forEach((item, index) => {
    lines.push(`- index: ${index} | detectedName: ${item.detectedName} | publisher: ${item.publisher} | launcher: ${item.launcher} | exeName: ${item.exeName}`);
  });

  return {
    // Items reach this prompt by several routes - no IGDB match, an uncorroborated one, IGDB
    // rate-limited, or the stage skipped entirely - so it cannot lean on IGDB having run.
    // Everything needed to separate a program from the game sharing its name comes from the
    // machine's own metadata, which is sent with every item.
    systemInstruction: {
      parts: [{
        text: [
          "Classify Windows programs and return only actual video games from the provided list.",
          "Exclude launchers, redistributables, tools, editors, drivers, anti-cheat, mods,",
          "dedicated servers, benchmarks, installers, and anything uncertain.",
          "Some installed programs share their name with a video game. Decide which one is",
          "actually installed using the publisher, launcher and executable given for each item,",
          "not the name alone. A program whose publisher is a software or services company, or",
          "that is installed standalone rather than through a game launcher, is not the game",
          "that happens to share its name."
        ].join(" ")
      }]
    },
    contents: [{ role: "user", parts: [{ text: lines.join("\n") }] }],
    generationConfig: {
      responseMimeType: "application/json",
      responseJsonSchema: {
        type: "object",
        properties: {
          games: {
            type: "array",
            items: {
              type: "object",
              properties: {
                index: { type: "integer" },
                detectedName: { type: "string" },
                canonicalTitle: { type: "string" }
              },
              required: ["index", "detectedName", "canonicalTitle"]
            }
          }
        },
        required: ["games"]
      },
      temperature: 0.1,
      maxOutputTokens: 2048
    },
    store: false
  };
}

// Returns detectedName (lowercased) -> canonicalTitle for the items Gemini called games.
function parseClassificationResponse(batch, payload) {
  const text = payload
    && Array.isArray(payload.candidates)
    && payload.candidates[0]
    && payload.candidates[0].content
    && Array.isArray(payload.candidates[0].content.parts)
    && payload.candidates[0].content.parts[0]
    && payload.candidates[0].content.parts[0].text;

  if (!text) {
    throw new Error("Gemini response contained no classification text");
  }

  const parsed = JSON.parse(text);
  if (!parsed || !Array.isArray(parsed.games)) {
    throw new Error("Gemini response contained no games array");
  }

  const named = new Map();

  for (const game of parsed.games) {
    if (!game || typeof game !== "object" || !Number.isInteger(game.index)) {
      continue;
    }

    const expected = batch[game.index];
    const detectedName = String(game.detectedName || "").trim();

    // Same guard the desktop client applied: an index/name pair that does not match what
    // we sent means the model drifted, and the row is dropped rather than misattributed.
    if (!expected || expected.detectedName.toLowerCase() !== detectedName.toLowerCase()) {
      continue;
    }

    const canonicalTitle = String(game.canonicalTitle || "").trim();
    named.set(expected.detectedName.toLowerCase(), canonicalTitle || expected.detectedName);
  }

  return named;
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