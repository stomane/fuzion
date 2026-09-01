using System;
using System.Configuration;
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
            public string GoogleSearchProxyUrl { get; set; }
            public string IgdbProxyUrl { get; set; }
            public string DbPassword { get; set; }
            public string SteamApiKey { get; set; }
            public string GeminiApiKey { get; set; }
            public string GeminiProxyUrl { get; set; }
            public string GeminiModel { get; set; }
            public string SentryDsn { get; set; }
        }

        private static readonly LocalSecrets localSecrets = LoadLocalSecrets();

        public static string gSearchApiKey = GetConfiguredValue(
            "GOOGLE_SEARCH_API_KEY",
            localSecrets?.GoogleSearchApiKey,
            "GoogleSearchAPIKey.txt");

        public static string gSearchProxyUrl = GetConfiguredValue(
            "GOOGLE_SEARCH_PROXY_URL",
            localSecrets?.GoogleSearchProxyUrl,
            null,
            "GoogleSearchProxyUrl");

        public static string igdbProxyURL = GetConfiguredValue(
            "IGDB_PROXY_URL",
            localSecrets?.IgdbProxyUrl,
            "IGDBProxyURL.txt",
            "IgdbProxyUrl");

        public static string dbPassword = GetConfiguredValue(
            "DB_PASSWORD",
            localSecrets?.DbPassword,
            "dbPassword.txt");

        public static string geminiApiKey = GetConfiguredValue(
            "GEMINI_API_KEY",
            localSecrets?.GeminiApiKey,
            null);

        public static string geminiProxyUrl = GetConfiguredValue(
            "GEMINI_PROXY_URL",
            localSecrets?.GeminiProxyUrl,
            null,
            "GeminiProxyUrl");

        public static string geminiModel = GetConfiguredValue(
            "GEMINI_MODEL",
            localSecrets?.GeminiModel,
            null);

        public static string sentryDsn = GetConfiguredValue(
            "SENTRY_DSN",
            localSecrets?.SentryDsn,
            null);

        public static bool HasGoogleSearchAccess => !string.IsNullOrWhiteSpace(gSearchProxyUrl) || !string.IsNullOrWhiteSpace(gSearchApiKey);
        public static bool HasGoogleSearchApiKey => HasGoogleSearchAccess;
        public static bool HasIgdbProxyUrl => !string.IsNullOrWhiteSpace(igdbProxyURL);
        public static bool HasGeminiAccess => !string.IsNullOrWhiteSpace(geminiProxyUrl) || !string.IsNullOrWhiteSpace(geminiApiKey);
        public static bool HasGeminiApiKey => HasGeminiAccess;
        public static bool UseGoogleSearchProxy => !string.IsNullOrWhiteSpace(gSearchProxyUrl);
        public static bool UseGeminiProxy => !string.IsNullOrWhiteSpace(geminiProxyUrl);
        public static string BackendBaseUrl => string.IsNullOrWhiteSpace(igdbProxyURL) ? string.Empty : igdbProxyURL.Trim().TrimEnd('/');
        public static string GeminiModel => string.IsNullOrWhiteSpace(geminiModel) ? "gemini-3.6-flash" : geminiModel.Trim();

        // Sentry DSNs are safe to ship in client code (they only permit sending events into
        // this project, not reading data back out), so unlike the keys above this one is fine
        // hardcoded as the default - override via SENTRY_DSN or local.secrets.json for a fork
        // pointing at a different Sentry project.
        public static string SentryDsn => string.IsNullOrWhiteSpace(sentryDsn)
            ? "https://955a4c0748791bbb5bf4de351a000015@o4512004309647360.ingest.de.sentry.io/4512004322426960"
            : sentryDsn.Trim();

        public static bool IsOfflineMode => !HasGoogleSearchAccess && !HasIgdbProxyUrl && !HasGeminiAccess;

        public static string BuildGeminiGenerateContentUrl()
        {
            if (UseGeminiProxy)
            {
                return AppendQueryString(geminiProxyUrl, "model=" + Uri.EscapeDataString(GeminiModel));
            }

            return "https://generativelanguage.googleapis.com/v1beta/models/"
                + Uri.EscapeDataString(GeminiModel)
                + ":generateContent?key="
                + Uri.EscapeDataString(geminiApiKey);
        }

        public static string BuildGoogleImageSearchUrl(string query, int num, bool preferTransparentIcons)
        {
            string parameters = preferTransparentIcons
                ? "fields=items/link&st=y&tbm=isch&epq=&oq=&eq=&cr=&tbs=ic:trans,iar:s&searchType=image&num=" + num
                : "fields=items/link&searchType=image&num=" + num;

            return BuildGoogleSearchUrlCore(query, parameters);
        }

        public static string BuildGoogleWikipediaLookupUrl(string query)
        {
            return BuildGoogleSearchUrlCore(query, "fields=items/link&num=1");
        }

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

        private static string GetConfiguredValue(string environmentVariableName, string localSecretValue, string legacyFileName, string appSettingKey = null)
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

            if (!string.IsNullOrWhiteSpace(appSettingKey))
            {
                string appSettingValue = ReadAppSetting(appSettingKey);
                if (!string.IsNullOrWhiteSpace(appSettingValue))
                {
                    return appSettingValue.Trim();
                }
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

        private static string ReadAppSetting(string key)
        {
            try
            {
                string value = ConfigurationManager.AppSettings[key];
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string BuildGoogleSearchUrlCore(string query, string parameters)
        {
            string encodedQuery = Uri.EscapeDataString(query ?? string.Empty);
            string queryString = parameters + "&q=" + encodedQuery;

            if (UseGoogleSearchProxy)
            {
                return AppendQueryString(gSearchProxyUrl, queryString);
            }

            return "https://www.googleapis.com/customsearch/v1/siterestrict?"
                + queryString
                + "&key="
                + gSearchApiKey;
        }

        private static string AppendQueryString(string baseUrl, string queryString)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return string.Empty;
            }

            string trimmedUrl = baseUrl.Trim();
            string separator = trimmedUrl.Contains("?")
                ? (trimmedUrl.EndsWith("?") || trimmedUrl.EndsWith("&") ? string.Empty : "&")
                : "?";

            return trimmedUrl + separator + queryString;
        }
    }
}
