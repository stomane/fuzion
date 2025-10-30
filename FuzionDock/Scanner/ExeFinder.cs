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
using SharpCompress.Common;

namespace Fuzion.Scanner
{
    internal class ExeFinder
    {
        static readonly List<string> unwantedExeNameStrings = new List<string>
            {
                "_",
                "x64",
                "x32",
                "-",
                "shipping",
                "win64",
                "win32",
                "game",
                "start"
            };

        private class TemporaryExeHolder
        {
            public List<string> ExePaths { get; set; } = new List<string>();
            public List<int> LevenshteinDistances { get; set; } = new List<int>();
            public List<int> Score { get; set; } = new List<int>();
        }

        public static string RemoveUnwantedStrings(string str, List<string> omitStrings = null)
        {
            System.Globalization.CultureInfo cultureInfo = new System.Globalization.CultureInfo("en-US", false);

            List<string> selectiveUnwantedStrings = unwantedExeNameStrings.ToList();

            if (omitStrings != null)
            {
                foreach (string s in omitStrings)
                {
                    if (selectiveUnwantedStrings.Contains(s))
                    {
                        selectiveUnwantedStrings.Remove(s);
                    }
                }
            }

            string result = str;

            foreach (string s in selectiveUnwantedStrings)
            {
                if (cultureInfo.CompareInfo.IndexOf(str, s, System.Globalization.CompareOptions.IgnoreCase) >= 0)
                {
                    result = Regex.Replace(result, s, "", RegexOptions.IgnoreCase);
                }
            }

            return result;
        }

        public static string GetExePath(string path, Program program) // needs to take only one argument
        {
            try
            {
                // Assign variables
                WebRequest webRequest;
                Stream stream;
                TemporaryExeHolder tempExeHolder = new TemporaryExeHolder();
                string[] exeArray = Directory.GetFiles(path, "*.exe", SearchOption.AllDirectories);

                // Main loop
                foreach (string exePath in exeArray)
                {
                    tempExeHolder.ExePaths.Add(exePath);
                    tempExeHolder.LevenshteinDistances.Add(100);
                    tempExeHolder.Score.Add(0);
                    int lastIndex = tempExeHolder.ExePaths.Count - 1;

                    int lvDist;
                    string strippedExe = System.IO.Path.GetFileNameWithoutExtension(exePath);
                    strippedExe = RemoveUnwantedStrings(strippedExe, new List<string> { "game" });
                    
                    // Old Url
                    //string url = "https://www.googleapis.com/customsearch/v1?fields=items/link&key=&num=1&q=" + strippedExe + " wikipedia";
                    //string url = "https://www.googleapis.com/customsearch/v1/siterestrict?fields=items/link&key=&num=1&q=" + strippedExe + " wikipedia";
                    string url = $"https://www.googleapis.com/customsearch/v1/siterestrict?fields=items/link&key={Constants.gSearchApiKey}&num=1&q={strippedExe} wikipedia";

                    lvDist = LevenshteinDistance.Compute(strippedExe.ToUpperInvariant(), program.DisplayName.ToUpperInvariant().Replace(" ", "")); //compare stripped exe to game name

                    //First check the exes locally, switch to ContainsAllWords if this is causing issues
                    if (program.DisplayName.ContainsMostWords(strippedExe))
                    {
                        tempExeHolder.Score[lastIndex]++;
                    }

                    if (lvDist <= 2)
                    {
                        tempExeHolder.Score[lastIndex]++;
                    }

                    if (lvDist == 0)
                    {
                        tempExeHolder.Score[lastIndex]++;
                    }

                    if (program.DisplayName.ToAcronym().ToUpperInvariant().Contains(strippedExe.ToUpperInvariant())) // acronym contains
                    {
                        tempExeHolder.Score[lastIndex]++;
                    }

                    if (program.DisplayName.ToAcronym().ToUpperInvariant().Equals(strippedExe.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase)) // acronym is
                    {
                        tempExeHolder.Score[lastIndex]++;
                    }

                    //Then check online if score doesn't add up
                    if (tempExeHolder.Score[lastIndex] < 3)
                    {
                        try
                        {
                            Uri uri = new Uri(url);
                            webRequest = WebRequest.Create(uri);
                            stream = webRequest.GetResponse().GetResponseStream();

                            StreamReader streamReader = new StreamReader(stream);

                            string streamLine = "";
                            int i = 0;

                            while (streamLine != null)
                            {
                                i++;
                                streamLine = streamReader.ReadLine();

                                if (streamLine != null && streamLine.Contains("link"))
                                {
                                    if (streamLine.Contains("wikipedia"))
                                    {
                                        //if (streamLine.ToUpper().Contains("SOFTWARE")) //Move software check before looking for exe in a separate method - WAS ENABLED
                                        //{
                                        //    isSoftwareList[game] = true; //mark it as software to remove later
                                        //}

                                        string requestResult = Regex.Match(streamLine, @"(?<=wiki/).*$").ToString();
                                        requestResult = Regex.Replace(requestResult, "_", " ");
                                        requestResult = requestResult.Remove(requestResult.Length - 1, 1);

                                        //Console.WriteLine($"Reference for exe is: {strippedExe} is: {requestResult}");

                                        lvDist = LevenshteinDistance.Compute(requestResult.ToUpperInvariant(), program.DisplayName.ToUpperInvariant().Replace(" ", "")); //compare result from wiki to game name

                                        if (lvDist <= 2)
                                        {
                                            tempExeHolder.Score[lastIndex]++;
                                        }

                                        if (lvDist == 0)
                                        {
                                            tempExeHolder.Score[lastIndex]++;
                                        }
                                    }
                                }
                            }
                            Console.ReadLine(); // obsolete?
                            streamReader.Close();
                        }
                        catch (WebException)
                        {

                        }
                    }
                    else
                    {
                        break;
                    }
                }

                Console.WriteLine($"Listing exe scan scores for path: {path}");
                for (int i = 0; i < tempExeHolder.Score.Count; i++)
                {
                    Console.WriteLine($"{tempExeHolder.ExePaths[i]} with score: {tempExeHolder.Score[i]}");
                }

                // Evaluate score
                int maxIndex = tempExeHolder.Score.IndexOf(tempExeHolder.Score.Max());
                // Set exe name immediately if one was found so database can pick it up
                program.ExeName = Path.GetFileName(tempExeHolder.ExePaths[maxIndex]);
                return tempExeHolder.ExePaths[maxIndex];
            }
            catch (Exception)
            {
                return program.WorkDir;
            }

        }

        static class LevenshteinDistance
        {
            public static int Compute(string s, string t)
            {
                if (string.IsNullOrEmpty(s))
                {
                    if (string.IsNullOrEmpty(t))
                        return 0;
                    return t.Length;
                }

                if (string.IsNullOrEmpty(t))
                {
                    return s.Length;
                }

                int n = s.Length;
                int m = t.Length;
                int[,] d = new int[n + 1, m + 1];

                // initialize the top and right of the table to 0, 1, 2, ...
                for (int i = 0; i <= n; d[i, 0] = i++) ;
                for (int j = 1; j <= m; d[0, j] = j++) ;

                for (int i = 1; i <= n; i++)
                {
                    for (int j = 1; j <= m; j++)
                    {
                        int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                        int min1 = d[i - 1, j] + 1;
                        int min2 = d[i, j - 1] + 1;
                        int min3 = d[i - 1, j - 1] + cost;
                        d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                    }
                }
                return d[n, m];
            }
        }

        //public void GetExeParallel()
        //{
        //    foreach (string path in gamePaths) //create the temporary holders for each game
        //    {
        //        isSoftwareList.Add(false);
        //    }
        //    //throw new Exception();
        //    Parallel.ForEach(gamePaths, (path, state, index) =>
        //    {
        //        if (LocalGameData.PathType[Convert.ToInt32(index)] != "URI")
        //        {
        //            GetExe(path, Convert.ToInt32(index));
        //        }
        //    });
        //}

        private void SetSpecificExe(string workDir, string fileName, int index)
        {
            //gamePaths[index] = GetSpecificExe(workDir, fileName, index);
        }

        private string GetSpecificExe(string workDir, string fileName, Program prog)
        {
            string[] exeArray = Directory.GetFiles(workDir, "*.exe", SearchOption.AllDirectories);

            foreach (string exePath in exeArray)
            {
                if (Path.GetFileName(exePath) == fileName)
                {
                    return exePath;
                }
            }

            //if not, look for best exe
            return GetExePath(workDir, prog);
        }
    }
}
