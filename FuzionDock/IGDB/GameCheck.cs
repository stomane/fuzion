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
            System.Diagnostics.Debug.WriteLine("[GameCheck] Preloading batch game decisions...");
            GeminiGameClassifier.ClassificationResult classification = GeminiGameClassifier.ClassifyGames(programs);

            System.Diagnostics.Debug.WriteLine($"[GameCheck] Batch preload: {classification.Games.Count} games, {classification.EvaluatedNames.Count} evaluated");

            lock (batchDecisionLock)
            {
                // Only programs Gemini actually finished classifying count as "known" here.
                // Anything it never evaluated (skipped, or its batch's API call failed) falls
                // through to the local DB / IGDB checks below instead of being assumed not-a-game.
                batchKnownPrograms = classification.EvaluatedNames;
                batchGamePrograms = classification.Games;
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

            System.Diagnostics.Debug.WriteLine($"[GameCheck] Starting IsGame check for: {prog.DisplayName}");
            int trueCount = 0;
            int falseCount = 0;

            // Has it been preset from launcher scans?
            if (prog.IsGame)
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: already marked as game from launcher");
                return true;
            }

            // Is it a false positive?
            if (prog.DisplayName.IsFalsePositive())
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: is false positive");
                prog.IsGame = false;
                return false;
            }

            // Is it marked as a game in the local game database?
            if (LocalDatabase.IsGame(prog.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: in local game database");
                trueCount++;
            }

            // Is it marked as a program in the local program database?
            if (LocalDatabase.IsProgram(prog.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: in local program database");
                falseCount++;
            }

            // Check Gemini batch decisions first
            if (TryGetBatchDecision(prog, out bool batchIsGame))
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: resolved by Gemini batch classifier: {batchIsGame}");
                prog.IsGame = batchIsGame;
                return batchIsGame;
            }

            // Is it in the Fuzion online database?
            if (SQL.DbConnection.GameExistsInDatabase(prog.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: in Fuzion online game database");
                trueCount++;
            }

            // Is it in the Fuzion Programs database?
            if (SQL.DbConnection.ProgramExistsInDatabase(prog.DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: in Fuzion online program database");
                falseCount++;
            }

            System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: scores - true: {trueCount}, false: {falseCount}");

            // Evaluate scores
            if (trueCount > falseCount && trueCount >= 2)
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: determined GAME by score");
                prog.IsGame = true;
                return true;
            }
            else if (falseCount > trueCount && falseCount >= 2)
            {
                System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: determined PROGRAM by score");
                prog.IsGame = false;
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: score inconclusive, falling back to IGDB");
            bool igdbResult = IsIGDB(prog.DisplayName);
            System.Diagnostics.Debug.WriteLine($"[GameCheck] {prog.DisplayName}: IGDB result = {igdbResult}");
            prog.IsGame = igdbResult;
            return igdbResult;
        }

        public static bool IsIGDB(string name)
        {
            if (!Constants.HasIgdbProxyUrl)
            {
                System.Diagnostics.Debug.WriteLine("Skipping IGDB lookup because offline mode is active.");
                return false;
            }

            // Check IGDB
            #region IGDB Check
            string comparisonLine;
            string comparisonName = name.ToLowerWithoutIGDBStrings();

            Uri uri = new Uri($"{Constants.igdbProxyURL}/production/v4/games?fields=name&limit=5&search={comparisonName}");

            System.Diagnostics.Debug.WriteLine("<<< IGDB IsGame Check Start >>>");
            System.Diagnostics.Debug.WriteLine("Sending string is: " + comparisonName);

            try
            {
                WebRequest webRequest = WebRequest.Create(uri);
                using (WebResponse webResponse = webRequest.GetResponse())
                using (Stream objStream = webResponse.GetResponseStream())
                using (StreamReader streamReader = new StreamReader(objStream))
                {
                    string streamLine;
                    while ((streamLine = streamReader.ReadLine()) != null)
                    {
                        System.Diagnostics.Debug.WriteLine("IGDB Line Read: " + streamLine);

                        if (streamLine.ToLowerInvariant().Contains("\"name\""))
                        {
                            comparisonLine = streamLine.ToLowerInvariant();//streamLine.ToLowerNormalized();
                            System.Diagnostics.Debug.WriteLine("Game result is: " + comparisonLine);

                            Regex regex = new Regex("(?<=\")(.*?)(?=\")"); // between "" but excluding "
                            MatchCollection matches = regex.Matches(comparisonLine);

                            comparisonLine = matches[matches.Count - 1].ToString(); //the last match
                            System.Diagnostics.Debug.WriteLine("Regexed IGDB name is: " + comparisonLine);
                            System.Diagnostics.Debug.WriteLine("Local game name is: " + comparisonName);

                            if (comparisonName.ContainsMostWords(comparisonLine, 40))
                            {
                                System.Diagnostics.Debug.WriteLine($"{name} returned True in IGDB");
                                return true;
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("<<< IGDB IsGame Check End >>>");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception in IsGame: " + ex.Message);
            }
            #endregion
            return false;
        }
    }
}
