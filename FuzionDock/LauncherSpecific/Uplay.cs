using Fuzion.Programs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Fuzion.LauncherSpecific
{
    static class Uplay
    {
        // Uplay silent argument should be but is not: /S
        public static bool Exists { get; } = UplayExists();
        public static bool ShadowLaunchEnabled { get; set; }
        public static string Path { get; set; }
        public static string WorkDir { get; set; }
        public static Process UplayProcess;
        public const string ClientProcessName = "upc"; // Uplay Client
        public static string Arguments { get; } = "-uplay_silent";

        // need to get 32bit view of registry and check there for installed games under *RegLocation\Launcher\Installs\ 'Game items here'
        //private static string RegLocation = @"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ubisoft";
        private const string RegLocation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Uplay";
        private const string GamesRegLocation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        private static bool UplayExists()
        {
            // new method which uses 32bit registry
            try
            {
                // Opens the registry in 32bit mode since in 64bits battle.net uninstall entry is under Wow6432Node Key
                using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    // goes to the uninstall entry on the battle.net client and retrieves the InstallLocation key to get the path
                    using (RegistryKey uplayUninstallKey = registry.OpenSubKey(RegLocation))
                    {
                        if(uplayUninstallKey != null)
                        {
                            Path = uplayUninstallKey.GetValue("DisplayIcon").ToString();
                            WorkDir = uplayUninstallKey.GetValue("InstallLocation").ToString();
                            return true;
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

        public static void OpenUplay()
        {
            UplayProcess = new Process();
            UplayProcess.StartInfo.FileName = Path;
            UplayProcess.StartInfo.WorkingDirectory = WorkDir;
            UplayProcess.Start();
            // Should dispose right after starting
        }

        public static List<Program> GetUplayGames()
        {
            List<Program> resultList = new List<Program>();

            if (Exists)
            {
                try
                {
                    using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    {
                        using (RegistryKey uninstall = registry.OpenSubKey(GamesRegLocation))
                        {
                            foreach (var subkey in uninstall.GetSubKeyNames())
                            {
                                if(subkey.Contains("Uplay Install"))
                                {
                                    var gameKey = registry.OpenSubKey(System.IO.Path.Combine(GamesRegLocation, subkey));


                                    string name = gameKey.GetValue("DisplayName").ToString();
                                    string appId = subkey.Replace("Uplay Install ", "");
                                    //Console.WriteLine("Uplay game found with app ID: " + appId);
                                    string sysIcon = gameKey.GetValue("DisplayIcon").ToString();
                                    string workDir = gameKey.GetValue("InstallLocation").ToString();
                                    string uninstallPath = gameKey.GetValue("UninstallString").ToString();

                                    // Example URI for Uplay: uplay://launch/{UplayAppID}/0
                                    Program p = new Program()
                                    {
                                        IsGame = true,
                                        DisplayName = name,
                                        Path = @"uplay://launch/" + appId + @"/0",
                                        SystemIcon = sysIcon,
                                        WorkDir = workDir,
                                        UninstallPath = uninstallPath,
                                        PathType = PathType.URI,
                                        Launcher = BelongsToLauncher.Uplay
                                    };

                                    resultList.Add(p);
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
            return resultList;
        }

        public static void UplayUpdateProgramObjects(List<Program> uplayGamesList, List<Program> listToUpdate)
        {
            List<string> uplayNamesList = new List<string>();

            foreach (Program prog in uplayGamesList)
            {
                uplayNamesList.Add(prog?.DisplayName);
            }

            List<string> programNamesList = new List<string>();
            foreach (Program prog in listToUpdate)
            {
                programNamesList.Add(prog?.DisplayName);
            }

            // Takes into consideration that programObjects will always find Steam games using normal scan, should be upgraded to check independently
            for (int i = 0; i < programNamesList.Count; i++)
            {
                //Console.WriteLine("Steam looking for: " + programNamesList[i]);

                if (uplayNamesList.Contains(programNamesList[i]))
                {
                    Console.WriteLine("Adding Uplay specifics to program: " + listToUpdate[i].DisplayName);

                    // Try catch block as .First might not be found, although I have a check whether it's in the list beforehand
                    try
                    {
                        int s = uplayGamesList.IndexOf(uplayGamesList.First(game => game.DisplayName == programNamesList[i]));

                        listToUpdate[i].IsGame = uplayGamesList[s].IsGame;
                        listToUpdate[i].DisplayName = uplayGamesList[s].DisplayName;
                        listToUpdate[i].Path = uplayGamesList[s].Path;
                        listToUpdate[i].SystemIcon = uplayGamesList[s].SystemIcon;
                        listToUpdate[i].WorkDir = uplayGamesList[s].WorkDir;
                        listToUpdate[i].UninstallPath = uplayGamesList[s].UninstallPath;
                        listToUpdate[i].PathType = uplayGamesList[s].PathType;
                        listToUpdate[i].Launcher = uplayGamesList[s].Launcher;

                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        // Try maximizing then closing, currently has issues when in tray or something. If this doesn't work, launch Uplay before launching with URI.
        // Also check game arguments when starting a game from the uplay launcher instead of shortcut
        //// WORKS
        //int i = 0;
        ////IntPtr[] handles = Native.NativeMethods.EnumerateProcessWindowHandles(process.Id).ToArray();

        //// close it when it shows up, limit to 10k ticks
        //while (i < 10000 && process != null)
        //{
        //    Console.WriteLine("Attempting to close Uplay: "+i);
        //    //Native.NativeMethods.ShowWindow(process.MainWindowHandle, Native.NativeMethods.SW_SHOWMAXIMIZED);
        //    //Native.NativeMethods.SetForegroundWindow(process.MainWindowHandle);
        //    process.CloseMainWindow();

        //    i++;
        //}
        //// WORKS

        public static void Close()
        {
            try
            {
                Process process = Process.GetProcessesByName(ClientProcessName).First();

                if (process != null)
                {
                    // Make uplay show
                    OpenUplay();

                    // New method using dispatcher
                    DispatcherTimer cd = new DispatcherTimer();
                    cd.Tick += CloseUplay_Tick;
                    cd.Interval = TimeSpan.FromMilliseconds(50d);

                    closeTickCount = 0;
                    cd.Start();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Uplay process is missing or closed");
            }
           
            
        }

        private static int closeTickCount = 0;

        private static void CloseUplay_Tick(object sender, EventArgs e)
        {
            try
            {
                Process process = Process.GetProcessesByName(ClientProcessName).First();

                if (closeTickCount < 600 && process != null)
                {
                    Console.WriteLine("Close Uplay Dispatcher tick: " + closeTickCount);
                    process.CloseMainWindow();
                    closeTickCount++;
                }
                else
                {
                    Console.WriteLine("Close Uplay Dispatcher FINISHED");
                    var disp = sender as DispatcherTimer;
                    disp.Stop();
                    UplayProcess.Dispose();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Close Uplay Dispatcher FINISHED");
                var disp = sender as DispatcherTimer;
                disp.Stop();
                UplayProcess.Dispose();
            }
           
        }
    }
}
