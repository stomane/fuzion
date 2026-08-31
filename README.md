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

Fuzion has been published to the Windows Store as a UWP app and a Standalone app, but those published builds are currently not showing the dock on newer versions of Windows 10/11, probably because of changes in Desktop rendering. The latest version in this repository has been updated and confirmed working on Windows 11 - build it yourself following the instructions below rather than using the Store builds.

**Windows Store Links:**
- [Fuzion Dock (UWP)](https://apps.microsoft.com/detail/9MTL580GPQ00?hl=en-us&gl=US&ocid=pdpshare)
- [Fuzion Dock (Standalone)](https://apps.microsoft.com/detail/XP8C9QP4X6CN53?hl=en-US&gl=US&ocid=pdpshare)

## Looking for a Maintainer

I'm looking for someone who wants to seriously take on the project and manage the public repository. If you're interested, please contact me using the Fuzion Discord: https://discord.gg/KQRrT6JBUv

## Support

If you'd like to support development, you can do so on Ko-fi: https://ko-fi.com/fuzion

## Configuration

Every key below is optional - games can always be added manually from the UI regardless of configuration, and Fuzion runs fine with zero setup, starting in offline mode with local detection only. For the best experience, we recommend setting up:

- A **Gemini API key** (from Google AI Studio) - classifies detected programs into actual games via an LLM.
- A **Google Custom Search API key** - fetches game icons/artwork automatically.

An IGDB proxy is also supported as an alternative/additional way to classify games, and the legacy remote database and Steam key are optional fallbacks that can safely be left unset.

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
- `IGDB_PROXY_URL`
- `DB_PASSWORD`
- `REDDIT_CLIENT_ID`
- `IGDB_CLIENT_ID`
- `IGDB_CLIENT_SECRET`

Those values are not required for the project to build, but some runtime features will not work without them.

For local development, Fuzion now also supports an ignored file at [FuzionDock/local.secrets.example.json](FuzionDock/local.secrets.example.json): create `FuzionDock/local.secrets.json` next to the project file and the build will copy it to the output directory automatically. Environment variables still take precedence over the local file.

The current app uses:

- `GoogleSearchApiKey` for online icon and executable lookup
- `IgdbProxyUrl` for IGDB-backed game detection
- `DbPassword` for the legacy remote database path
- `GeminiApiKey` for batched LLM-based game classification fallback
- `GeminiModel` to override the default `gemini-3.6-flash`

The current codebase does not consume a Steam API key directly; Steam store search uses the `SteamStoreQuery` package without a project-specific local key.

Gemini setup for this repo:

1. Open [Google AI Studio](https://aistudio.google.com/apikey).
2. Import the Google Cloud project you want to bill against if it is not already visible.
3. Create a new Gemini API key there. New keys are auth keys by default.
4. Restrict the key to Gemini API only.
5. Put the value in `FuzionDock/local.secrets.json` as `GeminiApiKey` or set `GEMINI_API_KEY` in your environment.

The current implementation uses Gemini structured JSON output through the Gemini `generateContent` API to classify a batch of detected programs and return only actual games.

Google Custom Search (icons) setup for this repo:

1. Get an API key from the [Google Cloud Console](https://console.cloud.google.com/apis/credentials) with the Custom Search API enabled.
2. Create a search engine at the [Programmable Search Engine control panel](https://programmablesearchengine.google.com/), with "Search the entire web" enabled and Image Search turned on, then copy its Search Engine ID (`cx`). A search engine restricted to specific sites (or without image search enabled) will silently return zero icon results.
3. Combine both values as `<API_KEY>&cx=<SEARCH_ENGINE_ID>` and put that whole string in `FuzionDock/local.secrets.json` as `GoogleSearchApiKey`, or set `GOOGLE_SEARCH_API_KEY` in your environment the same way. The code appends this value directly as the request's `key=` parameter, so both parts need to be combined into one string.

If the API keys are missing, Fuzion now starts in a plain offline mode: local launcher detection and local icons still work, while Google image search and IGDB lookups are skipped.

## License

See [LICENSE](LICENSE) file for details.
