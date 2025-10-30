using System;

namespace Fuzion
{
    public static class Constants
    {
        public static string gSearchApiKey = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY") ?? "";
        public static string igdbProxyURL = Environment.GetEnvironmentVariable("IGDB_PROXY_URL") ?? "";
        public static string dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
    }
}
