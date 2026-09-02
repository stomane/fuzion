using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Fuzion.Programs;
using Newtonsoft.Json;

namespace Fuzion.SQL
{
    class FuzionDB
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public class GameResult
        {
            public int id { get; set; }
            public string gamename { get; set; }
            public string iconlink { get; set; }
            public string exename { get; set; }
            public bool falsepositive { get; set; }
            public int iconrelevance { get; set; }
        }

        public class DBGameObject
        {
            public bool status { get; set; }
            public List<GameResult> result { get; set; }
        }

        public class ProgramResult
        {
            public int id { get; set; }
            public string name { get; set; }
            public string iconlink { get; set; }
            public string exename { get; set; }
            public bool falsepositive { get; set; }
        }

        public class DBProgramObject
        {
            public bool status { get; set; }
            public List<ProgramResult> result { get; set; }
        }

        public class PushGame
        {
            public string gameName { get; set; }
            public string iconLink { get; set; }
            public string exeName { get; set; }
            public int iconRelevance { get; set; }
        }

        public class PushProgram
        {
            public string name { get; set; }
            public string iconLink { get; set; }
            public string exeName { get; set; }
        }

        public static Tuple<string, int> GetIconTuple(string gameName, bool fPositive = false)
        {
            var result = Tuple.Create(string.Empty, 0);

            try
            {
                DBGameObject dbObj = GetGameObject(gameName, fPositive);

                if (dbObj?.result != null && dbObj.result.Count > 0 && !string.IsNullOrWhiteSpace(dbObj.result[0].iconlink))
                {
                    result = Tuple.Create(dbObj.result[0].iconlink, dbObj.result[0].iconrelevance);
                    Console.WriteLine("Returning Tuple from FUZION DB:");
                    Console.WriteLine(result.ToString());
                }
            }
            catch (Exception)
            {
                return result;
            }

            return result;
        }

        public static bool GameExistsInDatabase(string gameName, bool fPositive = false)
        {
            var dbObj = GetGameObject(gameName, fPositive);
            return dbObj != null && dbObj.status;
        }

        public static bool ProgramExistsInDatabase(string progName, bool fPositive = false)
        {
            try
            {
                var dbObj = GetProgramObject(progName, fPositive);
                return dbObj != null && dbObj.status;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async void PushList(List<Game> gamesList)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Constants.BackendBaseUrl))
                {
                    return;
                }

                var pushGamesList = new List<PushGame>();
                for (int i = 0; i < gamesList.Count; i++)
                {
                    pushGamesList.Add(new PushGame
                    {
                        gameName = gamesList[i].DisplayName,
                        exeName = gamesList[i].ExeName,
                        iconLink = gamesList[i].IconURI,
                        iconRelevance = 10
                    });
                }

                string jsonString = JsonConvert.SerializeObject(pushGamesList);
                var values = new Dictionary<string, string>
                {
                    { "data", jsonString }
                };

                var response = await httpClient.PostAsync(
                    Constants.BackendBaseUrl + "/insert/main",
                    new FormUrlEncodedContent(values)).ConfigureAwait(false);

                string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine("Fuzion DB Response for pushing Games List: " + responseString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fuzion DB push failed: " + ex.Message);
            }
        }

        public static async void PushList(List<Program> programList)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Constants.BackendBaseUrl))
                {
                    return;
                }

                var pushPrograms = new List<PushProgram>();
                for (int i = 0; i < programList.Count; i++)
                {
                    pushPrograms.Add(new PushProgram
                    {
                        name = programList[i].DisplayName,
                        iconLink = programList[i].IconURI,
                        exeName = programList[i].ExeName
                    });
                }

                string jsonString = JsonConvert.SerializeObject(pushPrograms);
                var values = new Dictionary<string, string>
                {
                    { "data", jsonString }
                };

                var response = await httpClient.PostAsync(
                    Constants.BackendBaseUrl + "/insert/program",
                    new FormUrlEncodedContent(values)).ConfigureAwait(false);

                string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine("Fuzion DB Response for pushing Programs List: " + responseString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fuzion DB push failed: " + ex.Message);
            }
        }

        private static DBGameObject GetGameObject(string gameName, bool fPositive)
        {
            int falsePositive = fPositive ? 1 : 0;
            var uri = new Uri(Constants.BackendBaseUrl + "/get/main?gamename=" + Uri.EscapeDataString(gameName ?? string.Empty) + "&falsepositive=" + falsePositive);
            string jsonData = httpClient.GetStringAsync(uri).GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<DBGameObject>(jsonData);
        }

        private static DBProgramObject GetProgramObject(string progName, bool fPositive)
        {
            int falsePositive = fPositive ? 1 : 0;
            var uri = new Uri(Constants.BackendBaseUrl + "/get/program?programname=" + Uri.EscapeDataString(progName ?? string.Empty) + "&falsepositive=" + falsePositive);
            string jsonData = httpClient.GetStringAsync(uri).GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<DBProgramObject>(jsonData);
        }
    }
}
