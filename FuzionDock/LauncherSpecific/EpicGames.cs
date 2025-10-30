using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using Fuzion.Programs;
using Microsoft.Win32;
using static Fuzion.Programs.ProgramManager;

namespace Fuzion.LauncherSpecific
{
    static class EpicGames
    {
        public static bool Exists { get; set; } = EpicGamesExists();
        public static bool ShadowLaunchEnabled { get; set; }
        public static string Path { get; private set; }
        public static string WorkDir { get; private set; }
        public static string ProgramDataPath { get; private set; }
        public const string ExeName = "EpicGamesLauncher.exe";
        public const string ClientProcessName = "EpicGamesLauncher";
        public const string Arguments = "-silent";
        public const string RegistryLocation = @"SOFTWARE\Epic Games\EpicGamesLauncher"; //32bit registry location holds only ProgramData location

        private static bool EpicGamesExists()
        {
            Path = Scanner.Scan.GetEpicGamesInstallLocationFromRegistry();

            if (!string.IsNullOrEmpty(Path))
            {
                Path = System.IO.Path.Combine(Path, "Launcher", "Engine", "Binaries", "Win32", ExeName);
                WorkDir = System.IO.Path.Combine(Path, "Launcher", "Engine", "Binaries", "Win32");
                ProgramDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic");
                Console.WriteLine("Epic Program Data Path is: " + ProgramDataPath);
                return true;
            }

            return false;
        }

        private static string GetLaunchPath()
        {
            string result = "";
            string registryKeyString = @"SOFTWARE\WOW6432Node\EpicGames";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKeyString))
            {
                foreach (string subkey_name in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        object currentInstallLocation = subkey.GetValue("INSTALLDIR");

                        if (currentInstallLocation != null && Directory.Exists(currentInstallLocation.ToString()))
                        {
                            if (Native.NativeMethods.is64BitOperatingSystem)
                            {
                                result = currentInstallLocation.ToString() + @"Launcher\Engine\Binaries\Win64\EpicGamesLauncher.exe";
                            } else
                            {
                                result = currentInstallLocation.ToString() + @"Launcher\Engine\Binaries\Win32\EpicGamesLauncher.exe";
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Epic games launcher path: " + result);
            return result;
        }

        public static List<Program> GetEpicGames()
        {
            List<Program> resultList = new List<Program>();

            if (Exists)
            {
                List<string> fromLauncherDat = FromLauncherDat();
                List<Program> fromItems = FromItems();

                if(fromItems != null && fromItems.Count > 0)
                {
                    foreach (Program prog in fromItems)
                    {
                        if (fromLauncherDat.Contains(prog.EpicAppName))
                        {
                            if (!string.IsNullOrEmpty(prog.WorkDir) && !string.IsNullOrEmpty(prog.ExeName))
                            {
                                //System.IO.Path.Combine(prog.WorkDir, prog.ExeName);
                                prog.SystemIcon = prog.WorkDir + @"\\" + prog.ExeName;
                            }
                            resultList.Add(prog);
                        }
                    }
                }
            }
            return resultList;
        }

        public static void EpicUpdateProgramObjects(List<Program> epicGamesList, List<Program> listToUpdate)
        {
            foreach (Program program in epicGamesList)
            {
                if(!listToUpdate.Contains(program))
                    listToUpdate.Add(program);
            }
        }

        private static List<string> FromLauncherDat()
        {
            List<string> listOfGames = new List<string>();


            string readThisPath = System.IO.Path.Combine(ProgramDataPath, "UnrealEngineLauncher", "LauncherInstalled.dat");

            try
            {
                StreamReader objReader = new StreamReader(readThisPath);

                string streamLine = "";
                int i = 0;

                string appName = "";
                string installLocation = "";

                while (streamLine != null)
                {
                    i++;
                    streamLine = objReader.ReadLine();

                    if (streamLine != null && streamLine.Contains("AppName"))
                    {
                        MatchCollection matches = Regex.Matches(streamLine, "\"[^\"]*\"");
                        appName = matches[1].ToString().Trim('"');

                        listOfGames.Add(appName);
                    }

                    if (streamLine != null && streamLine.Contains("InstallLocation"))
                    {
                        MatchCollection matches = Regex.Matches(streamLine, "\"[^\"]*\"");
                        installLocation = matches[1].ToString().Trim('"');
                        Console.WriteLine("Install Location: " + installLocation);
                    }
                }
                Console.ReadLine();
                objReader.Close();
            }
            catch (IOException)
            {

            }

            return listOfGames;
        }

        private static List<Program> FromItems()
        {
            List<Program> foundGamesList = new List<Program>();

            try
            {
                string readThisPath = System.IO.Path.Combine(ProgramDataPath, "EpicGamesLauncher", "Data", "Manifests");

                string[] files = Directory.GetFiles(readThisPath, "*.item", SearchOption.TopDirectoryOnly);
                foreach (string file in files) //read files here
                {
                    Program prog = new Program
                    {
                        Launcher = BelongsToLauncher.Epic,
                        OriginalLauncher = BelongsToLauncher.Epic,
                        PathType = PathType.URI,
                        OriginalPathType = PathType.URI,
                        IsGame = true
                    };

                    try
                    {
                        StreamReader objReader = new StreamReader(file);

                        string streamLine = "";
                        int i = 0;

                        while (streamLine != null)
                        {
                            i++;
                            streamLine = objReader.ReadLine();

                            if (streamLine != null && streamLine.Contains("AppName"))
                            {
                                // Example format: com.epicgames.launcher://apps/Curry?action=launch&silent=true
                                MatchCollection matches = Regex.Matches(streamLine, "\"[^\"]*\"");
                                prog.EpicAppName = matches[1].ToString().Trim('"');
                                prog.Path = $"com.epicgames.launcher://apps/{prog.EpicAppName}?action=launch&silent=true"; //&silent = true removed because of the launcher manager feature
                            }

                            if (streamLine != null && streamLine.Contains("LaunchExecutable"))
                            {
                                MatchCollection matches = Regex.Matches(streamLine, "\"[^\"]*\"");
                                prog.ExeName = matches[1].ToString().Trim('"');

                            }

                            if (streamLine != null && streamLine.Contains("DisplayName"))
                            {
                                MatchCollection matches = Regex.Matches(streamLine, "\"[^\"]*\"");
                                prog.DisplayName = matches[1].ToString().Trim('"');
                            }

                            if (streamLine != null && streamLine.Contains("InstallLocation"))
                            {
                                MatchCollection matches = Regex.Matches(streamLine, "\"[^\"]*\"");
                                prog.WorkDir = matches[1].ToString().Trim('"');
                            }

                        }
                        Console.ReadLine();
                        objReader.Close();
                    }
                    catch (IOException)
                    {

                    }

                    foundGamesList.Add(prog);
                }
            }
            catch (Exception)
            {

            }

            return foundGamesList;
        }

        public static void OpenEpicGames()
        {
            Process epicProcess = new Process();
            epicProcess.StartInfo.FileName = Path;
            epicProcess.StartInfo.WorkingDirectory = WorkDir;
            epicProcess.Start();
            epicProcess.Dispose();
        }

        public static void Close()
        {
            try
            {
                Process process = Process.GetProcessesByName(ClientProcessName).First();

                if (process != null)
                {
                    // Make epic show
                    OpenEpicGames();

                    // New method using dispatcher
                    DispatcherTimer cd = new DispatcherTimer();
                    cd.Tick += CloseEpic_Tick;
                    cd.Interval = TimeSpan.FromMilliseconds(50d);

                    closeTickCount = 0;
                    cd.Start();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Epic process is missing or closed");
            }


        }

        private static int closeTickCount = 0;

        private static void CloseEpic_Tick(object sender, EventArgs e)
        {
            try
            {
                Process process = Process.GetProcessesByName(ClientProcessName).First();

                if (closeTickCount < 600 && process != null)
                {
                    Console.WriteLine("Close Epic Dispatcher tick: " + closeTickCount);
                    process.CloseMainWindow();
                    closeTickCount++;
                }
                else
                {
                    Console.WriteLine("Close Epic Dispatcher FINISHED");
                    var disp = sender as DispatcherTimer;
                    disp.Stop();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Close Epic Dispatcher FINISHED");
                var disp = sender as DispatcherTimer;
                disp.Stop();
            }

        }
    }
}
