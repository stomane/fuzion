using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fuzion.AI;
using Fuzion.Extensions;
using Fuzion.Programs;

namespace Fuzion.IGDB
{
    class GameCheck
    {
        private static readonly object batchDecisionLock = new object();
        private static HashSet<string> batchKnownPrograms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> batchGamePrograms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void PreloadBatchGameDecisions(IEnumerable<Program> programs)
        {
            Console.WriteLine("[GameCheck] Preloading batch game decisions...");
            Dictionary<string, string> detectedGames = GeminiGameClassifier.ClassifyGames(programs).ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            HashSet<string> candidates = new HashSet<string>(
                programs
                    .Where(program => program != null && !string.IsNullOrWhiteSpace(program.DisplayName))
                    .Select(program => program.DisplayName),
                StringComparer.OrdinalIgnoreCase);

            Console.WriteLine($"[GameCheck] Batch preload: {detectedGames.Count} games, {candidates.Count} candidates");

            lock (batchDecisionLock)
            {
                batchKnownPrograms = candidates;
                batchGamePrograms = detectedGames;
            }
        }

        private static bool TryGetBatchDecision(Program prog, out bool isGame)
        {
            isGame = false;

            if (prog == null || string.IsNullOrWhiteSpace(prog.DisplayName))
            {
                return false;
            }

            lock (batchDecisionLock)
            {
                if (!batchKnownPrograms.Contains(prog.DisplayName))
                {
                    return false;
                }

                if (batchGamePrograms.TryGetValue(prog.DisplayName, out string canonicalTitle))
                {
                    if (string.IsNullOrWhiteSpace(prog.DockName) || string.Equals(prog.DockName, prog.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        prog.DockName = canonicalTitle;
                    }

                    isGame = true;
                    return true;
                }

                isGame = false;
                return true;
            }
        }

        //this returns a bool which says whether it's a game or not, needs to check local data and other places too
        // Will not return partial matches, have to try changing that

            /// <summary>
            /// Performs every possible check to see whether this program is a game. Checks for null.
            /// </summary>
            /// <param name="prog">Check this program to see if it's a game.</param>
            /// <returns></returns>
        public static bool IsGame(Program prog)
        {
            if (prog == null)
            {
                return false;
            }

            Console.WriteLine($"[GameCheck] Starting IsGame check for: {prog.DisplayName}");
            int trueCount = 0;
            int falseCount = 0;

            // Has it been preset from launcher scans?
            if (prog.IsGame)
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: already marked as game from launcher");
                return true;
            }

            // Is it a false positive?
            if (prog.DisplayName.IsFalsePositive())
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: is false positive");
                prog.IsGame = false;
                return false;
            }

            // Is it marked as a game in the local game database?
            if (LocalDatabase.IsGame(prog.DisplayName))
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: in local game database");
                trueCount++;
            }

            // Is it marked as a program in the local program database?
            if (LocalDatabase.IsProgram(prog.DisplayName))
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: in local program database");
                falseCount++;
            }

            // Check Gemini batch decisions first
            if (TryGetBatchDecision(prog, out bool batchIsGame))
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: resolved by Gemini batch classifier: {batchIsGame}");
                prog.IsGame = batchIsGame;
                return batchIsGame;
            }

            // Is it in the Fuzion online database?
            if (SQL.DbConnection.GameExistsInDatabase(prog.DisplayName))
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: in Fuzion online game database");
                trueCount++;
            }

            // Is it in the Fuzion Programs database?
            if (SQL.DbConnection.ProgramExistsInDatabase(prog.DisplayName))
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: in Fuzion online program database");
                falseCount++;
            }

            Console.WriteLine($"[GameCheck] {prog.DisplayName}: scores - true: {trueCount}, false: {falseCount}");

            // Evaluate scores
            if (trueCount > falseCount && trueCount >= 2)
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: determined GAME by score");
                prog.IsGame = true;
                return true;
            }
            else if (falseCount > trueCount && falseCount >= 2)
            {
                Console.WriteLine($"[GameCheck] {prog.DisplayName}: determined PROGRAM by score");
                prog.IsGame = false;
                return false;
            }

            Console.WriteLine($"[GameCheck] {prog.DisplayName}: score inconclusive, falling back to IGDB");
            bool igdbResult = IsIGDB(prog.DisplayName);
            Console.WriteLine($"[GameCheck] {prog.DisplayName}: IGDB result = {igdbResult}");
            prog.IsGame = igdbResult;
            return igdbResult;
        }

        private static string GetUTFString(string str)
        {
            byte[] bytes = System.Text.Encoding.Default.GetBytes(str);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public static bool IsIGDB(string name)
        {
            if (!Constants.HasIgdbProxyUrl)
            {
                Console.WriteLine("Skipping IGDB lookup because offline mode is active.");
                return false;
            }

            // Check IGDB
            #region IGDB Check
            string comparisonLine;
            string comparisonName = name.ToLowerWithoutIGDBStrings(); //name.ToLowerInvariant(); //name.ToLowerNormalized();

            //comparisonName = GetUTFString(comparisonName);

            //Uri uri = new Uri("https://api-v3.igdb.com/games/?fields=name&limit=5&search=" + comparisonName);

            // New IGDB link
            Uri uri = new Uri($"{Constants.igdbProxyURL}/production/v4/games?fields=name&limit=5&search={comparisonName}");

            WebRequest webRequest;
            Stream objStream;
            Console.WriteLine("<<< IGDB IsGame Check Start >>>");
            Console.WriteLine("Sending string is: " + comparisonName);

            try
            {
                webRequest = WebRequest.Create(uri);
                objStream = webRequest.GetResponse().GetResponseStream();
                StreamReader streamReader = new StreamReader(objStream);

                string streamLine = "";
                int i = 0;

                while (streamLine != null)
                {
                    i++;
                    streamLine = streamReader.ReadLine();
                    Console.WriteLine("IGDB Line Read: " + streamLine);

                    if (streamLine != null && streamLine.ToLowerInvariant().Contains("\"name\""))
                    {

                        comparisonLine = streamLine.ToLowerInvariant();//streamLine.ToLowerNormalized();
                        Console.WriteLine("Game result is: " + comparisonLine);

                        Regex regex = new Regex("(?<=\")(.*?)(?=\")"); // between "" but excluding "
                        MatchCollection matches = regex.Matches(comparisonLine);

                        comparisonLine = matches[matches.Count - 1].ToString(); //the last match
                        Console.WriteLine("Regexed IGDB name is: " + comparisonLine);
                        Console.WriteLine("Local game name is: " + comparisonName);

                        if (comparisonName.ContainsMostWords(comparisonLine, 40))
                        {
                            streamReader.Close();
                            Console.WriteLine($"{name} returned True in IGDB");
                            return true;
                        }
                    }
                }

                //Console.WriteLine("<<< IGDB IsGame Check End >>>");
                Console.ReadLine();
                streamReader.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in IsGame: " + ex.Message);
            }
            #endregion
            //prog.IsGame = false;
            return false;
        }

      
    }
}
