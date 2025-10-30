using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Fuzion.Programs;
using Newtonsoft.Json;

namespace Fuzion.SQL
{
    class FuzionDB
    {
        public class GameResult
        {
            public int id { get; set; }
            public string gamename { get; set; }
            public string iconlink { get; set; }
            public string exename { get; set; }
            public bool falsepositive { get; set; }
            public bool iconrelevance { get; set; }
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

        //public static bool GameExistsInDatabase(string gameName, bool fPositive = false)
        //{
        //    // Request
        //    WebRequest webRequest;
        //    Stream stream;
        //    int falsePositive = fPositive ? 1 : 0;
        //    Uri rUri = new Uri("http://api.fuzion.gg:8040/get/main?gamename=" + gameName + "&falsepositive="+falsePositive);

        //    webRequest = WebRequest.Create(rUri);
        //    stream = webRequest.GetResponse().GetResponseStream();
        //    StreamReader streamReader = new StreamReader(stream);
        //    string jsonData = streamReader.ReadToEnd();

        //    var dbObj = JsonConvert.DeserializeObject<DBObject>(jsonData);

        //    Console.WriteLine("DB OBJ:");
        //    Console.WriteLine("Status: " + dbObj.status);
        //    Console.WriteLine("Icon Link: " + dbObj.result[0].iconlink);

        //    Console.WriteLine($"Game Exists Test for {gameName} data:");
        //    Console.WriteLine(jsonData);

        //    streamReader.Close();
        //    stream.Close();

        //    return true;
        //}

        public static Tuple<string,int> GetIconTuple(string gameName, bool fPositive = false)
        {
            var result = Tuple.Create(string.Empty, 0);

            try
            {
                // Request
                WebRequest webRequest;
                Stream stream;
                int falsePositive = fPositive ? 1 : 0;
                Uri rUri = new Uri("http://api.fuzion.gg:8040/get/main?gamename=" + gameName + "&falsepositive=" + falsePositive);

                webRequest = WebRequest.Create(rUri);
                stream = webRequest.GetResponse().GetResponseStream();
                StreamReader streamReader = new StreamReader(stream);
                string jsonData = streamReader.ReadToEnd();

                var dbObj = JsonConvert.DeserializeObject<DBGameObject>(jsonData);

                streamReader.Close();
                stream.Close();

                if(dbObj.result[0].iconlink.Length != 0)
                {
                    result = Tuple.Create(dbObj.result[0].iconlink, 10);
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
            // Request
            WebRequest webRequest;
            Stream stream;
            int falsePositive = fPositive ? 1 : 0;
            Uri rUri = new Uri("http://api.fuzion.gg:8040/get/main?gamename=" + gameName + "&falsepositive=" + falsePositive);

            webRequest = WebRequest.Create(rUri);
            stream = webRequest.GetResponse().GetResponseStream();
            StreamReader streamReader = new StreamReader(stream);
            string jsonData = streamReader.ReadToEnd();

            var dbObj = JsonConvert.DeserializeObject<DBGameObject>(jsonData);

            streamReader.Close();
            stream.Close();

            return dbObj.status;
        }

        public static bool ProgramExistsInDatabase(string progName, bool fPositive = false)
        {
            try
            {
                // Request
                WebRequest webRequest;
                Stream stream;
                int falsePositive = fPositive ? 1 : 0;
                Uri rUri = new Uri("http://api.fuzion.gg:8040/get/program?programname=" + progName + "&falsepositive=" + falsePositive);

                webRequest = WebRequest.Create(rUri);
                stream = webRequest.GetResponse().GetResponseStream();
                StreamReader streamReader = new StreamReader(stream);
                string jsonData = streamReader.ReadToEnd();

                var dbObj = JsonConvert.DeserializeObject<DBProgramObject>(jsonData);

                streamReader.Close();
                stream.Close();

                return dbObj.status;
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
                // Prepare the list for serialization
                List<PushGame> pushGamesList = new List<PushGame>();
                for (int i = 0; i < gamesList.Count; i++)
                {
                    PushGame pGame = new PushGame
                    {
                        gameName = gamesList[i].DisplayName,
                        exeName = gamesList[i].ExeName,
                        iconLink = gamesList[i].IconURI,
                        iconRelevance = 10
                    };

                    //// Check if iconLink is a valid URL - will add later
                    //bool result = Uri.TryCreate(pGame.iconLink, UriKind.Absolute, out Uri uriResult)
                    //    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                    //if(result)
                    pushGamesList.Add(pGame);
                }

                // Should move HTTPClient to a static field and initialize it only if necessary
                // Read more about HTTPClient on MSDN
                var client = new HttpClient();

                //var jsonString = "[{\"gameName\":\"TESTOVAIGRA\",\"iconLink\":\"testovaigra.com\",\"exeName\":\"hollowknight.exe\",\"iconRelevance\":\"10\"},{\"gameName\":\"TESTOVAIGRA2\",\"iconLink\":\"linktoicon.com\",\"exeName\":\"testovaigra2.exe\",\"iconRelevance\":\"10\"}]";
                var jsonString = JsonConvert.SerializeObject(pushGamesList);

                var values = new Dictionary<string, string>
                {
                    { "data" , jsonString }
                };

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync("http://api.fuzion.gg:8040/insert/main", content).ConfigureAwait(false);

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine("Fuzion DB Response for pushing Games List: "+responseString);
                Console.WriteLine("LIST PUSHED:");
                Console.WriteLine(jsonString);
                client.Dispose();
            }
            catch (Exception)
            {
               
            }
      
        }

        public static async void PushList(List<Program> programList)
        {
            try
            {
                // Should move HTTPClient to a static field and initialize it only if necessary
                // Read more about HTTPClient on MSDN
                var client = new HttpClient();

                // Sample JSON format
                //var jsonString = "[{\"gameName\":\"TESTOVAIGRA\",\"iconLink\":\"testovaigra.com\",\"exeName\":\"hollowknight.exe\",\"iconRelevance\":\"10\"},{\"gameName\":\"TESTOVAIGRA2\",\"iconLink\":\"linktoicon.com\",\"exeName\":\"testovaigra2.exe\",\"iconRelevance\":\"10\"}]";

                var jsonString = string.Empty;

                var values = new Dictionary<string, string>
                {
                    { "data" , jsonString }
                };

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync("http://api.fuzion.gg:8040/insert/main", content).ConfigureAwait(false);

                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                client.Dispose();
            }
            catch (Exception)
            {

            }

        }

    }
}
