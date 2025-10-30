using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fuzion.Extensions;
using Fuzion.Programs;

namespace Fuzion.IGDB
{
    class GameCheck
    {
        //this returns a bool which says whether it's a game or not, needs to check local data and other places too
        // Will not return partial matches, have to try changing that

            /// <summary>
            /// Performs every possible check to see whether this program is a game. Checks for null.
            /// </summary>
            /// <param name="prog">Check this program to see if it's a game.</param>
            /// <returns></returns>
        public static bool IsGame(Program prog)
        {
            int trueCount = 0;
            int falseCount = 0;

            if (prog != null)
            {
                // Has it been preset from launcher scans?
                //Console.WriteLine("Is marked game from previous scan?");
                if (prog.IsGame)
                {
                    Console.WriteLine($"{prog.DisplayName} Returned true already marked as game");
                    prog.IsGame = true;
                    return true;
                    //trueCount++;
                }

                // Is it a false positive?
                //Console.WriteLine("Is in false positive const list?");
                if (prog.DisplayName.IsFalsePositive())
                {
                    Console.WriteLine($"{prog.DisplayName} Returned false is false positive");
                    prog.IsGame = false;
                    return false;
                    //falseCount++;
                }

                // OLD
                //if (prog.DisplayName.IsGame())
                //{
                //    Console.WriteLine($"{prog.DisplayName} Returned true already marked as game in games.fzn");
                //    //prog.IsGame = true;
                //    //return true;
                //    trueCount++;
                //}

                // NEW
                // Is it marked as a game in the local game database?
                //Console.WriteLine("Is in games.fzn?");
                if (LocalDatabase.IsGame(prog.DisplayName))
                {
                    Console.WriteLine($"{prog.DisplayName} Returned true already marked as game in games.xml");
                    //prog.IsGame = true;
                    //return true;
                    trueCount++;
                }

                // Is it marked as a program in the local program database?
                //Console.WriteLine("Is in programs.fzn?");
                if (LocalDatabase.IsProgram(prog.DisplayName))
                {
                    Console.WriteLine($"{prog.DisplayName} Returned false already marked as program in programs.xml");
                    //prog.IsGame = false;
                    //return false;
                    falseCount++;
                }

                // Is it in the Fuzion online database?
                //Console.WriteLine("Is in Fuzion DB as GAME?");
                if (SQL.DbConnection.GameExistsInDatabase(prog.DisplayName))
                {
                    Console.WriteLine($"{prog.DisplayName} In Fuzion online game database");
                    //prog.IsGame = true;
                    //return true;
                    trueCount++;
                }

                // Is it in the Fuzion Programs database?
                //Console.WriteLine("Is in Fuzion DB as PROGRAM?");
                if (SQL.DbConnection.ProgramExistsInDatabase(prog.DisplayName))
                {
                    Console.WriteLine($"{prog.DisplayName} In Fuzion online program database");
                    //prog.IsGame = false;
                    //return false;
                    falseCount++;
                }

                Console.WriteLine($"{prog.DisplayName} game check true score: {trueCount} and false score: {falseCount}");

                // Evaluate scores
                if(trueCount != 0 || falseCount != 0)
                {
                    // Scored more trues
                    if(trueCount > falseCount && trueCount >= 2)
                    {
                        prog.IsGame = true;
                        return true;
                    }
                    else if (falseCount > trueCount && falseCount >= 2)
                    {
                        prog.IsGame = false;
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"{prog.DisplayName} resorted to IGDB");
                        // Check IGDB
                        bool result = IsIGDB(prog.DisplayName);
                        prog.IsGame = result;
                        return result;
                    }
                } 
                else
                {
                    Console.WriteLine($"{prog.DisplayName} resorted to IGDB");
                    // Check IGDB
                    bool result = IsIGDB(prog.DisplayName);
                    prog.IsGame = result;
                    return result;
                }
            }

            return false;
        }

        private static string GetUTFString(string str)
        {
            byte[] bytes = System.Text.Encoding.Default.GetBytes(str);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public static bool IsIGDB(string name)
        {
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
