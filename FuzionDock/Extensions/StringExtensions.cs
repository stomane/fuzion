using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
using Fuzion.Programs;
using static Fuzion.Programs.ProgramManager;

namespace Fuzion.Extensions
{
    public enum ComparisonType
    {
        Either,
        Or
    }

    public static class StringExtensions
    {
        static readonly List<string> falsePositiveProgramNames = new List<string>
            {
                "unity",
                "twitch",
                "origin",
                "steam",
                "uplay",
                "discord",
                "kodi",
                "fuzion",
                "battle.net",
                "your phone",
                "blender",
                "movies & tv",
                "office",
                "gog galaxy",
                "zoom",
                "get help",
                "redlauncher"
            };

        static readonly List<string> unwantedStringsForIGDBLookupList = new List<string>
            {
                "demo",
                "trial",
                "launcher",
                "for windows 10"
            };

        static readonly List<string> unwantedChars = new List<string> //list of chars to remove
            {
                //":",
                "-",
                "_",
                "\u00A9",
                "\u2122",
                "\u24C7"
            };

        public static string RemoveInvalidFileNameChars(this string str)
        {
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(str, invalidRegStr, "");
        }

        public enum FormattedFor { Full, IGDB }

        public static string ToLowerNormalized(this string str, FormattedFor ffor = FormattedFor.Full)
        {
            // Regex version - slower
            //Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            //str = rgx.Replace(str, "");
            string result = str?.ToLowerInvariant(); //lowercase all

            if (result.Contains(@"\u0027")) //replace that darn apostrophe
            {
                result = result.Replace(@"\u0027", "'");
            }

            foreach (string s in unwantedStringsForIGDBLookupList) //remove demo, trial, etc.
            {
                if (result.Contains(s))
                {
                    result = result.Replace(s, string.Empty);
                }
            }

            foreach (string s in unwantedChars) //remove : - _, etc.
            {
                if (result.Contains(s))
                {
                    result = result.Replace(s, " ");
                }
            }

            //remove all special characters
            char[] arr = result.ToCharArray();

            arr = Array.FindAll(arr, (c => (char.IsLetterOrDigit(c)
                                              || char.IsWhiteSpace(c)
                                              || c == '-'
                                              || c == '"')));

            result = new string(arr);




            return result;
        }

        public static string ToLowerWithoutIGDBStrings(this string str)
        {
            string result = str?.ToLowerInvariant(); //lowercase all

            foreach (string s in unwantedStringsForIGDBLookupList) //remove demo, trial, etc.
            {
                if (result.Contains(s))
                {
                    result = result.Replace(s, string.Empty);
                }
            }

            return result;
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static string ToAcronym(this string input)
        {
            string acronym = input.ToLowerNormalized();

            if (input != null)
            {
                acronym = string.Join(string.Empty, input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]));
            }

            return acronym;
        }

        /// <summary>
        /// Check if 2 strings are the same.
        /// </summary>
        /// <param name="firstString"></param>
        /// <param name="secondString"></param>
        /// <param name="type"></param>
        /// <returns>True if both strings match exactly</returns>
        public static bool ContainsAllWords(this string firstString, string secondString, ComparisonType type = ComparisonType.Either)
        {
            bool result = false;

            if (firstString != null && secondString != null)
            {
                firstString = firstString.ToUpperInvariant();
                secondString = secondString.ToUpperInvariant();

                string[] firstArray = firstString.Split();
                string[] secondArray = secondString.Split();

                if (type == ComparisonType.Either)
                {

                    if (secondArray.All(firstString.Contains)) //Does firstString contain all of secondArrays pieces
                    {
                        Console.WriteLine("First string contains all of second string for title: " + firstString);
                        result = true;
                    }

                    if (firstArray.All(secondString.Contains)) //The other way around
                    {
                        Console.WriteLine("Second string contains all of first string for title: " + firstString);
                        result = true;
                    }

                    return result;
                }

                if (type == ComparisonType.Or)
                {
                    if (secondArray.All(firstString.Contains)) //Does firstString contain all of secondArrays pieces
                    {
                        result = true;
                    }

                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Check if 2 strings partially match.
        /// </summary>
        /// <param name="firstString">First string to compare</param>
        /// <param name="secondString">Second string to compare</param>
        /// <param name="percentThreshold">How many percent constitute a match</param>
        /// <returns>True if both strings are more than 50% the same.</returns>
        public static bool ContainsMostWords(this string firstString, string secondString, int percentThreshold = 40)
        {
            bool result = false;

            if (firstString != null && secondString != null)
            {
                double percent = 0;
                int matchCount = 0;

                firstString = firstString.ToUpperInvariant();
                secondString = secondString.ToUpperInvariant();

                string[] firstArray = firstString.Split();
                string[] secondArray = secondString.Split();

                foreach (string str in firstArray)
                {
                    //Console.WriteLine("Word in 1st array: " + str);
                    if (secondArray.Contains(str))
                    {
                        matchCount++;
                    }
                }

                foreach (string str in secondArray)
                {
                    //Console.WriteLine("Word in 2nd array: "+ str);
                    if (firstArray.Contains(str))
                    {
                        matchCount++;
                    }
                }
                percent = ((double)matchCount / (firstArray.Length + secondArray.Length))*100;
                //Console.WriteLine("Percent match is: " + percent);
                if (percent >= percentThreshold)
                {
                    result = true;
                }

                return result;
            }

            return result;
        }

        public static bool IsFalsePositive(this string programName)
        {
            bool result = false;
            if (falsePositiveProgramNames.Contains(programName?.ToLowerInvariant()))
            {
                result = true;
            }

            return result;
        }

        public static bool IsDigitsOnly(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return false;
            } 
            else
            {
                foreach (char c in str)
                {
                    if (c < '0' || c > '9') return false;
                }

                return true;
            }
        }

        public static bool IsDuplicateGame(this string name, string path)
        {
            bool result = false;

            foreach (Game g in GameObjects)
            {
                if(g.DisplayName == name)
                {
                    result = true;
                }

                if(g.Path == path)
                {
                    result = true;
                }

                if (g.WorkDir == path)
                {
                    result = true;
                }
            }

            return result;
        }

        public static bool IsDuplicateGame(this Game game)
        {
            bool result = false;

            foreach (Game g in GameObjects)
            {
                if (g.DisplayName == game?.DisplayName)
                {
                    result = true;
                }

                if (g.Path == game?.Path)
                {
                    result = true;
                }

                if (g.WorkDir == game?.WorkDir)
                {
                    result = true;
                }

                if (g.Path == game?.WorkDir)
                {
                    result = true;
                }
            }

            return result;
        }

        public static bool IsDuplicateGame(this Program prog)
        {
            bool result = false;

            foreach (Game g in GameObjects)
            {
                if (g.DisplayName == prog?.DisplayName)
                {
                    result = true;
                }

                if (g.Path == prog?.Path)
                {
                    result = true;
                }

                if (g.WorkDir == prog?.WorkDir)
                {
                    result = true;
                }

                if (g.Path == prog?.WorkDir)
                {
                    result = true;
                }
            }

            return result;
        }

        public static bool IsProgram(this string progName)
        {
            return LocalDatabase.IsProgram(progName);
        }

        public static bool IsGame(this string progName)
        {
            return LocalDatabase.IsGame(progName);
        }
    }
}
