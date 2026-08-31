using System;
using System.IO;
using Newtonsoft.Json;

namespace Fuzion
{
    public static class Constants
    {
        private const string LocalSecretsFileName = "local.secrets.json";

        private sealed class LocalSecrets
        {
            public string GoogleSearchApiKey { get; set; }
            public string IgdbProxyUrl { get; set; }
            public string DbPassword { get; set; }
            public string SteamApiKey { get; set; }
            public string GeminiApiKey { get; set; }
            public string GeminiModel { get; set; }
            public string SentryDsn { get; set; }
        }

        private static readonly LocalSecrets localSecrets = LoadLocalSecrets();

        public static string gSearchApiKey = GetConfiguredValue(
            "GOOGLE_SEARCH_API_KEY",
            localSecrets?.GoogleSearchApiKey,
            "GoogleSearchAPIKey.txt");

        public static string igdbProxyURL = GetConfiguredValue(
            "IGDB_PROXY_URL",
            localSecrets?.IgdbProxyUrl,
            "IGDBProxyURL.txt");

        public static string dbPassword = GetConfiguredValue(
            "DB_PASSWORD",
            localSecrets?.DbPassword,
            "dbPassword.txt");

        public static string geminiApiKey = GetConfiguredValue(
            "GEMINI_API_KEY",
            localSecrets?.GeminiApiKey,
            null);

        public static string geminiModel = GetConfiguredValue(
            "GEMINI_MODEL",
            localSecrets?.GeminiModel,
            null);

        public static string sentryDsn = GetConfiguredValue(
            "SENTRY_DSN",
            localSecrets?.SentryDsn,
            null);

        public static bool HasGoogleSearchApiKey => !string.IsNullOrWhiteSpace(gSearchApiKey);
        public static bool HasIgdbProxyUrl => !string.IsNullOrWhiteSpace(igdbProxyURL);
        public static bool HasGeminiApiKey => !string.IsNullOrWhiteSpace(geminiApiKey);
        public static string GeminiModel => string.IsNullOrWhiteSpace(geminiModel) ? "gemini-3.6-flash" : geminiModel.Trim();

        // Sentry DSNs are safe to ship in client code (they only permit sending events into
        // this project, not reading data back out), so unlike the keys above this one is fine
        // hardcoded as the default - override via SENTRY_DSN or local.secrets.json for a fork
        // pointing at a different Sentry project.
        public static string SentryDsn => string.IsNullOrWhiteSpace(sentryDsn)
            ? "https://955a4c0748791bbb5bf4de351a000015@o4512004309647360.ingest.de.sentry.io/4512004322426960"
            : sentryDsn.Trim();

        public static bool IsOfflineMode => !HasGoogleSearchApiKey && !HasIgdbProxyUrl && !HasGeminiApiKey;

        private static LocalSecrets LoadLocalSecrets()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalSecretsFileName);

                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path).Trim();
                if (json.Length == 0)
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<LocalSecrets>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetConfiguredValue(string environmentVariableName, string localSecretValue, string legacyFileName)
        {
            string envValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue.Trim();
            }

            if (!string.IsNullOrWhiteSpace(localSecretValue))
            {
                return localSecretValue.Trim();
            }

            if (string.IsNullOrWhiteSpace(legacyFileName))
            {
                return string.Empty;
            }

            return ReadLegacySecretFile(legacyFileName);
        }

        private static string ReadLegacySecretFile(string fileName)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
