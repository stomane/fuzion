using Fuzion.Programs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fuzion.Extensions;
using static Fuzion.Programs.ProgramManager;
using System.Windows.Threading;

namespace Fuzion.LauncherSpecific
{
    static class Steam
    {
        public static bool Exists { get; private set; } = SteamExists();
        public static bool ShadowLaunchEnabled { get; set; }
        public static string Path { get; private set; }
        public static List<string> GameFolders { get; private set; }

        public static string WorkDir { get; private set; }
        public static string Arguments = "-silent";
        //private const string RegistryLocation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam";
        private const string RegistryLocation = @"SOFTWARE\Valve\Steam";
        public const string ExeName = "Steam.exe";
        public static Process SteamProcess = InitializeSteamProcess();
        public const string ClientProcessName = "steam";

        private static readonly List<string> steamToolsKeywords = new List<string>
            {
                "sdk",
                "dedicated",
                "server",
                "tool",
                "redistributable",
                "redistributables"
            };

        private static bool SteamExists()
        {
            // new method which uses 32bit registry
            try
            {
                // Opens the registry in 32bit mode since in 64bits steam uninstall entry is under Wow6432Node Key
                using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    using (RegistryKey steamKey = registry.OpenSubKey(RegistryLocation))
                    {
                        if(steamKey != null)
                        {
                            string installPath = steamKey.GetValue("InstallPath").ToString();

                            Path = System.IO.Path.Combine(installPath, ExeName);
                            WorkDir = installPath;

                            if (Directory.Exists(WorkDir) && File.Exists(Path))
                            {
                                GameFolders = GetLibraryFolders();
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static List<string> GetLibraryFolders()
        {
            List<string> result = new List<string>();

            if (Directory.Exists(System.IO.Path.Combine(WorkDir, "steamapps")))
            {
                result.Add(System.IO.Path.Combine(WorkDir, "steamapps"));
            }

            try
            {
                List<string> additionalFolders = ReadLibraryFoldersFile();

                if (additionalFolders.Count > 0)
                {
                    foreach (string path in additionalFolders)
                    {
                        result.Add(path);
                    }
                }
            }
            catch (Exception)
            {

            }


            return result;
        }

        private static List<string> ReadLibraryFoldersFile()
        {
            List<string> result = new List<string>();

            string[] files = Directory.GetFiles(System.IO.Path.Combine(WorkDir, "steamapps"), "*.vdf", SearchOption.TopDirectoryOnly);

            if (files.Length > 0)
            {
                foreach (string file in files)
                {
                    try
                    {
                        StreamReader objReader = new StreamReader(file);
                        string streamLine = objReader.ReadToEnd();

                        if (streamLine != null)
                        {
                            MatchCollection matchCollection = Regex.Matches(streamLine, "\"([^\"]*)\"");

                            foreach (Match match in matchCollection)
                            {
                                string path = match.Value.Trim('"');
                                string dir = System.IO.Path.Combine(path, "steamapps");


                                if (Directory.Exists(dir) && !result.Contains(dir))
                                {
                                    result.Add(System.IO.Path.Combine(path, "steamapps"));
                                    Console.WriteLine("Steam Library path: "+System.IO.Path.Combine(path, "steamapps"));
                                }
                            }
                        }

                        objReader.Close();
                    }
                    catch (Exception)
                    {

                    }
                }
            }



            return result;
        }

        public static List<Program> GetSteamGames()
        {
            // Refresh Library folders, might have changed
            GameFolders = GetLibraryFolders();

            List<Program> result = new List<Program>();

            if (Exists)
            {
                foreach (string steamAppsPath in GameFolders)
                {
                    //string steamAppsPath = WorkDir + @"\steamapps";

                    string[] files = Directory.GetFiles(steamAppsPath, "*.acf", SearchOption.TopDirectoryOnly);

                    foreach (string file in files)
                    {
                        string appID = Regex.Match(file, @"_([^;]*)\.").ToString();
                        Console.WriteLine("APP ID Regex is: " + appID);
                        appID = appID.Substring(1, appID.Length - 2);
                        Console.WriteLine("APP ID: " + appID);

                        try
                        {
                            StreamReader objReader = new StreamReader(file);

                            string streamLine = "";
                            int i = 0;

                            while (streamLine != null)
                            {
                                i++;
                                streamLine = objReader.ReadLine();

                                if (streamLine != null && streamLine.Contains("name"))
                                {
                                    MatchCollection matchCollection = Regex.Matches(streamLine, "\"([^\"]*)\"");
                                    string gameName = matchCollection[1].Value.Trim('"');

                                    if (!steamToolsKeywords.Contains(gameName.ToLowerInvariant()))
                                    {
                                        Console.WriteLine("Steam program found: " + gameName + " with APP ID: " + appID);
                                        Program steamGame = new Program()
                                        {
                                            DisplayName = gameName,
                                            Path = @"steam://rungameid/" + appID,
                                            OriginalPath = @"steam://rungameid/" + appID,
                                            SteamAppID = appID,
                                            Launcher = BelongsToLauncher.Steam,
                                            OriginalLauncher = BelongsToLauncher.Steam,
                                            PathType = PathType.URI,
                                            OriginalPathType = PathType.URI
                                        };

                                        if (!result.Contains(steamGame))
                                        {
                                            result.Add(steamGame);
                                        }
                                    }
                                }
                            }
                            Console.ReadLine();
                            objReader.Close();
                        }
                        catch (IOException)
                        {

                        }
                    }
                }

            }

            return result;
        }

        public static void SteamUpdateProgramObjects(List<Program> steamGamesList, List<Program> listToUpdate)
        {

            List<Program> existingSteamPrograms = steamGamesList.Where(g => listToUpdate.Any(p => p.DisplayName == g.DisplayName)).ToList();
            List<Program> missingSteamPrograms = steamGamesList.Where(g => !listToUpdate.Any(p => p.DisplayName == g.DisplayName)).ToList();

            if(existingSteamPrograms.Count > 0)
            {
                foreach (var item in existingSteamPrograms)
                {
                    Console.WriteLine("Adding Steam specifics to program: " + item.DisplayName);

                    // Try catch block as .First might not be found, although I have a check whether it's in the list beforehand
                    try
                    {
                        int i = listToUpdate.IndexOf(listToUpdate.First(p => p.DisplayName == item.DisplayName));

                        listToUpdate[i].SteamAppID = item.SteamAppID;
                        listToUpdate[i].Path = item.Path;
                        listToUpdate[i].OriginalPath = item.Path;
                        listToUpdate[i].PathType = item.PathType;
                        listToUpdate[i].OriginalPathType = item.PathType;
                        listToUpdate[i].Launcher = item.Launcher;
                        listToUpdate[i].OriginalLauncher = item.Launcher;
                        listToUpdate[i].IsGame = item.IsGame;
                    }
                    catch (Exception)
                    {

                    }
                }

            }

            if (missingSteamPrograms.Count > 0)
            {
                foreach (var item in missingSteamPrograms)
                {
                    Console.WriteLine("These DONT exist in program list: " + item.DisplayName);

                    listToUpdate.Add(item);
                }

            } else
            {
                Console.WriteLine("Missing Steam Programs SEQUENCE IS EMPTY");
            }

            //// Old method
            //List<string> steamNamesList = new List<string>();

            //foreach (Program prog in steamGamesList)
            //{
            //    steamNamesList.Add(prog.DisplayName);
            //}

            //List<string> programNamesList = new List<string>();
            //foreach (Program prog in listToUpdate)
            //{
            //    programNamesList.Add(prog.DisplayName);
            //}

            //// Takes into consideration that programObjects will always find Steam games using normal scan, should be upgraded to check independently
            //for (int i = 0; i < programNamesList.Count; i++)
            //{
            //    //Console.WriteLine("Steam looking for: " + programNamesList[i]);
            //    if (steamNamesList.Contains(programNamesList[i]))
            //    {
            //        Console.WriteLine("Adding Steam specifics to program: " + listToUpdate[i].DisplayName);

            //        // Try catch block as .First might not be found, although I have a check whether it's in the list beforehand
            //        try
            //        {
            //            int s = steamGamesList.IndexOf(steamGamesList.First(game => game.DisplayName == programNamesList[i]));

            //            listToUpdate[i].SteamAppID = steamGamesList[s].SteamAppID;
            //            listToUpdate[i].Path = steamGamesList[s].Path;
            //            listToUpdate[i].OriginalPath = steamGamesList[s].Path;
            //            listToUpdate[i].PathType = steamGamesList[s].PathType;
            //            listToUpdate[i].OriginalPathType = steamGamesList[s].PathType;
            //            listToUpdate[i].Launcher = steamGamesList[s].Launcher;
            //            listToUpdate[i].OriginalLauncher = steamGamesList[s].Launcher;
            //            listToUpdate[i].IsGame = steamGamesList[s].IsGame;
            //        }
            //        catch (Exception)
            //        {

            //        }
            //    } 
            //}
        }

        private static Process InitializeSteamProcess()
        {
            Process result = new Process();
            result.StartInfo.FileName = Path;
            //result.StartInfo.Arguments = Arguments;
            result.EnableRaisingEvents = true;
            result.OutputDataReceived += SteamProcess_OutputDataReceived;
            result.ErrorDataReceived += SteamProcess_ErrorDataReceived;
            result.Exited += SteamProcess_Exited;

            return result;
        }

        public static void OpenSteam()
        {
            SteamProcess.Start();

            DispatcherTimer pReadTimer = new DispatcherTimer();
            pReadTimer.Interval = TimeSpan.FromSeconds(1d);
            pReadTimer.Tick += PReadTimer_Tick;

        }

        private static void PReadTimer_Tick(object sender, EventArgs e)
        {
            if (SteamProcess.Handle != null)
            {
                SteamProcess.BeginOutputReadLine();
                SteamProcess.BeginErrorReadLine();
                DispatcherTimer disp = sender as DispatcherTimer;
                disp.Stop();
            }
        }

        private static void SteamProcess_Exited(object sender, EventArgs e)
        {
            Console.WriteLine("Steam Closed");
        }

        private static void SteamProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Steam error data: " + e.Data);
        }

        private static void SteamProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Steam output data: " + e.Data);
        }

        //public static void CloseSteam()
        //{
        //    Process[] processList = Process.GetProcessesByName("Steam");

        //    foreach (var item in processList)
        //    {
        //        Console.WriteLine("Process for steam found: " + item.ProcessName);
        //    }
        //    if (processList.Length != 0)
        //    {
        //        processList[0].Close();
        //    }
        //}

        public static void Close()
        {
            Process[] processList = Process.GetProcessesByName("Steam");

            foreach (var item in processList)
            {
                Console.WriteLine("Process for steam found: " + item.ProcessName);
            }
            if (processList.Length != 0)
            {
                Process result = new Process();
                result.StartInfo.FileName = Path;
                result.StartInfo.Arguments = "-shutdown";
                result.Start();
                result.Dispose();
            }
        }

        public static void IssueShutdown()
        {
            Process[] processList = Process.GetProcessesByName("Steam");

            foreach (var item in processList)
            {
                Console.WriteLine("Process for steam found: " + item.ProcessName);
            }
            if (processList.Length != 0)
            {
                Process p = new Process();
                p.StartInfo.FileName = Fuzion.MainWindow.DefaultAssetPath + @"\msghandler\SendMessage.exe";
                p.StartInfo.Arguments = @"/message:17 /windowhandle:" + processList[0].MainWindowHandle;
                p.Start();
                //p.Dispose();
            }
        }

        public static void Kill()
        {
            Process[] processList = Process.GetProcessesByName("Steam");

            if (processList.Length != 0)
            {
                processList[0].Kill();
                Icons.TrayIcon.RefreshTrayArea();
            }
        }
    }
}
