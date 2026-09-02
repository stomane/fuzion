# Fuzion Dock

Fuzion is a seamless game dock with automatic icon fetching and styling. It includes quite a few extra features such as deal fetching from reddit, omni search which searches in both steam and the dock, delayed and silent when available game launcher launching, and gamepad support. I no longer have the time to upkeep it.

## Features

Here are videos showing some of its features:

- [Feature Overview 1](https://www.youtube.com/watch?v=3J_RkttY18Y)
- [Feature Overview 2](https://www.youtube.com/watch?v=0SxQwtXNbsM)
- [Feature Overview 3](https://www.youtube.com/watch?v=EWxGA0x2igY)
- [Feature Overview 4](https://www.youtube.com/watch?v=I1nqhvt9KJk)
- [Feature Overview 5](https://www.youtube.com/watch?v=ta0jqpnwblw)
- [Feature Overview 6](https://www.youtube.com/watch?v=puYv7PPIAOk)

## Current Status

The Microsoft Store is the official place to get Fuzion:

**[Fuzion Dock on the Microsoft Store](https://apps.microsoft.com/detail/9MTL580GPQ00)**

The currently published build doesn't show the dock on newer versions of Windows 10/11, probably because of changes in Desktop rendering. The latest version in this repository has been updated and confirmed working on Windows 11, and is what the next Store release will ship - until then, build it yourself following the instructions below.

A second "Standalone" EXE listing used to exist and has been retired, so there's one listing to point people at. The Store handles installation and updates, so Fuzion has no in-app updater.

## Looking for a Maintainer

I'm looking for someone who wants to seriously take on the project and manage the public repository. If you're interested, please contact me using the Fuzion Discord: https://discord.gg/KQRrT6JBUv

## Support

If you'd like to support development, you can do so on Ko-fi: https://ko-fi.com/fuzion

## Configuration

Every key below is optional - games can always be added manually from the UI regardless of configuration, and Fuzion runs fine with zero setup, starting in offline mode with local detection only. For the best experience, we recommend setting up:

- A **Gemini API key** (from Google AI Studio) - classifies detected programs into actual games via an LLM.
- A **Google Custom Search API key** - fetches game icons/artwork automatically.

Game classification and metadata both go through the Fuzion backend, which fronts IGDB and Gemini and caches the results. The Steam key is an optional extra that can safely be left unset.

## Running In VS Code

This is a legacy WPF app targeting .NET Framework 4.6.2, so the correct build path in VS Code is MSBuild from Visual Studio or Build Tools, not `dotnet run`.

1. Clone the repository and open the repo folder (not the `FuzionDock` subfolder) in VS Code.
2. Install the recommended VS Code extension when prompted: `C#`.
3. Make sure Visual Studio 2022 or Visual Studio Build Tools is installed with MSBuild support.
4. In VS Code, run the default build task with `Ctrl+Shift+B`.
5. Press `F5` and choose `Debug Fuzion (.NET Framework)` to build and debug the app.
6. If you only want to launch without attaching a debugger, run the `Run Fuzion` task from `Tasks: Run Task`.
7. If the app is still running and a later build says `Fuzion.exe` is locked, run the `Stop Fuzion` task or press `Shift+F5` to stop debugging.

The app runs fine with no configuration at all (see below) - it just starts in offline mode.

Current task setup resolves `MSBuild.exe` automatically through `vswhere`, so it works across Community, Professional, and Build Tools installs.

`C# Dev Kit` does not support this project format because [FuzionDock/Fuzion.csproj](FuzionDock/Fuzion.csproj) is a traditional non-SDK-style .NET Framework WPF project. Use the standard `C#` extension for this workspace unless you decide to do a full SDK-style project migration.

To reduce language-server restore warnings for native/runtime-specific packages, the project now declares `RuntimeIdentifiers=win`.

The app reads configuration from environment variables now. The main ones currently referenced in code are:

- `GOOGLE_SEARCH_API_KEY`
- `GOOGLE_SEARCH_PROXY_URL`
- `IGDB_PROXY_URL`
- `GEMINI_API_KEY`
- `GEMINI_PROXY_URL`
- `REDDIT_CLIENT_ID`
- `IGDB_CLIENT_ID`
- `IGDB_CLIENT_SECRET`

Those values are not required for the project to build, but some runtime features will not work without them.

For local development, Fuzion now also supports an ignored file at [FuzionDock/local.secrets.example.json](FuzionDock/local.secrets.example.json): create `FuzionDock/local.secrets.json` next to the project file and Debug builds will copy it to the output directory automatically. Release and Store packaging builds do not copy that file, so local secrets do not get embedded into a submission by accident. Environment variables take precedence over the local file.

For published builds, safe non-secret defaults can also be shipped in [FuzionDock/App.config](FuzionDock/App.config) as `GeminiProxyUrl`, `GoogleSearchProxyUrl`, and `IgdbProxyUrl`. The app resolves configuration in this order: environment variable, `local.secrets.json`, then `App.config`, then any legacy text-file fallback.

The current app uses:

- `GoogleSearchApiKey` for online icon and executable lookup
- `GoogleSearchProxyUrl` for a release-safe online icon/executable lookup backend
- `IgdbProxyUrl` for IGDB-backed game detection
- `GeminiApiKey` for batched LLM-based game classification fallback
- `GeminiProxyUrl` for a release-safe Gemini backend
- `GeminiModel` to override the default `gemini-3.6-flash`

The current codebase does not consume a Steam API key directly; Steam store search uses the `SteamStoreQuery` package without a project-specific local key.

Gemini setup for this repo:

1. Open [Google AI Studio](https://aistudio.google.com/apikey).
2. Import the Google Cloud project you want to bill against if it is not already visible.
3. Create a new Gemini API key there. New keys are auth keys by default.
4. Restrict the key to Gemini API only.
5. For local development only, put the value in `FuzionDock/local.secrets.json` as `GeminiApiKey` or set `GEMINI_API_KEY` in your environment.

Do not ship a shared Gemini API key inside a published desktop build. Any key bundled into the client can be extracted and abused. Keep the real key server-side and point the app at your backend instead.

Game classification no longer calls Gemini from the client at all. The app posts a batch of detected programs to `POST /classify/programs` on the backend, which answers from its shared cache where it can and only reaches IGDB or Gemini for what is left - see [Running The Backend Locally](#running-the-backend-locally). The prompt, response schema and parsing all live server-side, so the client ships no Gemini contract. `GeminiProxyUrl` remains supported as a thin passthrough for anything that wants raw `generateContent` access.

Google Custom Search (icons) setup for this repo:

1. Get an API key from the [Google Cloud Console](https://console.cloud.google.com/apis/credentials) with the Custom Search API enabled.
2. Create a search engine at the [Programmable Search Engine control panel](https://programmablesearchengine.google.com/), with "Search the entire web" enabled and Image Search turned on, then copy its Search Engine ID (`cx`). A search engine restricted to specific sites (or without image search enabled) will silently return zero icon results.
3. Combine both values as `<API_KEY>&cx=<SEARCH_ENGINE_ID>` and put that whole string in `FuzionDock/local.secrets.json` as `GoogleSearchApiKey`, or set `GOOGLE_SEARCH_API_KEY` in your environment the same way. The code appends this value directly as the request's `key=` parameter, so both parts need to be combined into one string.

For a published build that should work for all users, do not ship the Custom Search key or engine ID in the client. Instead, point `GoogleSearchProxyUrl` in [FuzionDock/App.config](FuzionDock/App.config) at your backend. The app will send the same query parameters it already uses for Google Custom Search and expects the same JSON shape back, especially `items[].link`.

IGDB setup for this repo:

1. Create a Twitch developer application for the backend and use a confidential client type.
2. Keep the Twitch client ID and client secret server-side only.
3. Point `IgdbProxyUrl` in [FuzionDock/App.config](FuzionDock/App.config) at your backend base URL.

The current desktop client calls the proxy as `GET /production/v4/games?...`, and the backend translates those query parameters into IGDB's POST query format before calling Twitch / IGDB with a server-side app access token.

The backend service for the official app now lives in [FuzionBackend/package.json](FuzionBackend/package.json). It is intended for Cloud Run and uses Secret Manager for the live Gemini, IGDB, and Custom Search credentials.

`FuzionBackend` also supports an optional PostgreSQL cache, used for confirmed game/program names, is-game verdicts, and cached IGDB and Custom Search responses. Set either `CLOUD_SQL_CONNECTION_NAME` (Cloud Run) or `POSTGRES_HOST`/`POSTGRES_PORT` (anywhere else) along with `POSTGRES_DB`, `POSTGRES_USER` and `POSTGRES_PASSWORD`, and the service creates its tables on first start. Without a database the service still runs; it just caches nothing.

See [FuzionBackend/.env.example](FuzionBackend/.env.example) for the full environment contract, and [Running The Backend Locally](#running-the-backend-locally) for a working local setup.

If the API keys are missing, Fuzion now starts in a plain offline mode: local launcher detection and local icons still work, while Google image search and IGDB lookups are skipped.

## Running The Backend Locally

The desktop app talks to a small Node service in [FuzionBackend](FuzionBackend) that holds the
API keys server-side. You can run your own copy, so you never need anyone else's credentials -
nothing in this repository contains a real key.

Requires Node 20 or newer.

```
cd FuzionBackend
npm install
cp .env.example .env
```

Fill in whichever upstream credentials you have. All of them are optional: an endpoint whose
credential is missing returns an error, and everything else keeps working.

### Optional: a local database

The backend runs fine with no database - it simply stops caching. Adding one gives you the
shared caches (is-game verdicts, icon binaries, IGDB and Custom Search responses). Any
PostgreSQL will do:

```
docker run -d --name fuzion-pg -p 5432:5432 \
  -e POSTGRES_PASSWORD=devpassword -e POSTGRES_DB=fuzioncache postgres:16-alpine
```

Then set in `.env`:

```
POSTGRES_HOST=127.0.0.1
POSTGRES_PORT=5432
POSTGRES_DB=fuzioncache
POSTGRES_USER=postgres
POSTGRES_PASSWORD=devpassword
```

Tables are created automatically on first start. `CLOUD_SQL_CONNECTION_NAME` is the Cloud Run
path (a unix socket) and takes precedence when set, so leave it blank locally.

### Start it

`npm start` reads configuration from the real environment only. To load the `.env` file you
just created, use the local script instead - it passes Node's built-in `--env-file`, so no
extra dependency is involved:

```
npm run start:local
curl http://localhost:8080/health
```

Set `REQUEST_LOG=1` to log every request with its status and duration, which is the quickest
way to see what the desktop app is actually asking for.

### Point the desktop app at it

Create `FuzionDock/local.secrets.json`:

```json
{
  "IgdbProxyUrl": "http://127.0.0.1:8080",
  "GoogleSearchProxyUrl": "http://127.0.0.1:8080/custom-search",
  "GeminiProxyUrl": "http://127.0.0.1:8080/gemini"
}
```

`IgdbProxyUrl` doubles as the backend base URL, so it must be the origin with no path. Debug
builds copy this file to the output directory; Release and Store builds never do, so local
settings cannot end up in a submission.

### What the backend exposes

| Endpoint | Purpose |
| --- | --- |
| `POST /classify/programs` | Decides which detected programs are games |
| `GET /get/main`, `GET /get/program` | Cached game / program metadata |
| `POST /insert/main`, `POST /insert/program` | Push metadata discovered by a client |
| `GET /asset/<path>` | Icon binaries cached into Cloud Storage |
| `POST /gemini`, `GET /custom-search`, `/production/v4/games` | Thin upstream proxies |
| `GET /health` | Liveness and which features are configured |

`POST /classify/programs` is the one the desktop app leans on. It takes a batch of programs
with the metadata found on the machine:

```json
{ "items": [
  { "detectedName": "Enshrouded", "publisher": "Keen Games GmbH",
    "launcher": "Steam", "exeName": "unknown" }
] }
```

and resolves each one in order - shared cache first, then IGDB, then Gemini - caching every
verdict, so a second scan of the same library reaches neither upstream service. Programs it
could not judge come back under `unresolved` rather than as a negative, so the client falls
back to its own checks instead of trusting a non-answer.

Because a name alone is ambiguous (*Parsec* is both a 1982 TI-99 game and a remote-desktop
tool), an IGDB name match only counts when the installed publisher corroborates one of the
IGDB entry's companies. Anything uncorroborated goes to Gemini, which is given the publisher,
launcher and executable found on the machine and decides from those.

## Publishing To The Microsoft Store

Fuzion ships as a **packaged desktop app**: the same full-trust WPF process, wrapped in an MSIX via Desktop Bridge. It is not a UWP app, and doesn't need to be.

The manifest declares `uap10:RuntimeBehavior="packagedClassicApp"` with `uap10:TrustLevel="mediumIL"`, which means the process runs with the user's normal token at medium integrity and **not** inside an app container. That's what keeps the global keyboard/mouse hooks, XInput/DirectInput gamepad support, registry scanning and launcher folder access working exactly as they do unpackaged - the file/registry virtualization described in Microsoft's MSIX docs applies only to appContainer apps.

To build the package for submission, run the **Build Store Package (MSIX)** task in VS Code (or the equivalent below), then upload the resulting `.msixupload` in Partner Center:

```
msbuild FuzionPackaging\FuzionPackaging.wapproj /restore /t:Build ^
  /p:Configuration=Release /p:Platform=x64 ^
  /p:AppxPackageSigningEnabled=false /p:UapAppxPackageBuildMode=StoreUpload
```

Output lands in `FuzionPackaging\AppPackages\`. Signing is intentionally off - the Store re-signs the package with its own certificate, so no code signing certificate needs to be bought or maintained. (This is a real difference from an EXE/MSI listing, which requires you to Authenticode-sign the installer yourself *and* host it at a versioned HTTPS URL on your own CDN.)

Notes for maintainers:

- The `Identity` block in [FuzionPackaging/Package.appxmanifest](FuzionPackaging/Package.appxmanifest) must match Partner Center's *Product identity* page exactly, or the upload is rejected.
- Bump `Version` in that manifest for each submission, and keep `ApplicationVersion` in [FuzionDock/Fuzion.csproj](FuzionDock/Fuzion.csproj) in step with it. The Store requires the revision (fourth) part to be `0`.
- The regular `Any CPU` build does not build the package, so the normal edit/run loop stays fast.
- Launch-on-startup is declared as a `windows.startupTask` extension, so users can also toggle it from Settings > Apps > Startup.

## License

See [LICENSE](LICENSE) file for details.
