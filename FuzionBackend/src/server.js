const express = require("express");

const app = express();
const port = parseInt(process.env.PORT || "8080", 10);
const defaultGeminiModel = process.env.GEMINI_DEFAULT_MODEL || "gemini-3.5-flash-lite";
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

app.disable("x-powered-by");
app.use(express.json({ limit: "256kb" }));
app.use(express.text({ type: ["text/plain", "application/apicalypse"], limit: "32kb" }));

app.get("/health", (req, res) => {
  res.json({
    ok: true,
    service: "fuzion-backend",
    project: process.env.GOOGLE_CLOUD_PROJECT || null,
    region: process.env.GOOGLE_CLOUD_REGION || null
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
  const upstream = await fetch(`https://www.googleapis.com/customsearch/v1/siterestrict?${query.toString()}`);

  await relayResponse(upstream, res);
}));

app.all(["/production/v4/games", "/igdb/v4/games"], handleAsync(async (req, res) => {
  const clientId = getRequiredEnv("IGDB_TWITCH_CLIENT_ID");
  getRequiredEnv("IGDB_TWITCH_CLIENT_SECRET");

  const query = extractIgdbQuery(req);
  const upstream = await callIgdbGames(query, clientId);

  await relayResponse(upstream, res);
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
}

function copyHeader(upstream, res, headerName) {
  const value = upstream.headers.get(headerName);
  if (value) {
    res.set(headerName, value);
  }
}