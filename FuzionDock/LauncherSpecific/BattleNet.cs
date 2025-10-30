using Fuzion.Programs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.Extensions.StringExtensions;

namespace Fuzion.LauncherSpecific
{
    static class BattleNet
    {
        public static bool Exists = BattleNetExists();
        public static bool ShadowLaunchEnabled { get; set; }
        public static string Arguments { get; } = "--autostarted";
        public static string Path { get; private set; }
        public static string WorkDir { get; private set; }
        public static Process BattleNetProcess;
        public const string ClientProcessName = "Battle.net";

        private static bool BattleNetExists()
        {
            try
            {
                // Opens the registry in 32bit mode since in 64bits battle.net uninstall entry is under Wow6432Node Key
                using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    Console.WriteLine("Bnet base key location: "+ registry.ToString());
                    // goes to the uninstall entry on the battle.net client and retrieves the InstallLocation key to get the path
                    using (RegistryKey bnetUninstallKey = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Battle.net"))
                    {
                        if(bnetUninstallKey != null)
                        {
                            WorkDir = bnetUninstallKey.GetValue("InstallLocation").ToString();

                            if (System.IO.Directory.Exists(WorkDir) && System.IO.File.Exists(System.IO.Path.Combine(WorkDir, "Battle.net.exe")))
                            {
                                Path = System.IO.Path.Combine(WorkDir, "Battle.net.exe");
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

        public static readonly string[] LaunchCodesList = new string[]
            {
                "wow",
                "wowclassic",
                "wowptr",
                "d3",
                "d3ptr",
                "hs",
                "ow",
                "owptr",
                "sc2",
                "hots",
                "scr",
                "w3",
                "codbo4",
                "codmw2019"
            };

        public static readonly string[] BattleNetGameNamesList = new string[]
            {
                "WORLD OF WARCRAFT",
                "WORLD OF WARCRAFT CLASSIC",
                "WORLD OF WARCRAFT PTR",
                "DIABLO III",
                "DIABLO III PTR",
                "HEARTHSTONE",
                "OVERWATCH",
                "OVERWATCH PTR",
                "STARCRAFT II",
                "HEROES OF THE STORM",
                "STARCRAFT REMASTERED",
                "WARCRAFT III",
                "CALL OF DUTY: BLACK OPS 4",
                "CALL OF DUTY: MODERN WARFARE 2019"
            };

        public static void BattleNetUpdateProgramObjects(List<Program> listToUpdate)
        {
            for (int i = 0; i < listToUpdate.Count; i++)
            {
                for (int y = 0; y < BattleNetGameNamesList.Length; y++)
                {
                    if (listToUpdate[i].DisplayName.ToUpperInvariant().Contains(BattleNetGameNamesList[y]))
                    {
                        listToUpdate[i].Path = AppDomain.CurrentDomain.BaseDirectory + @"bnet\bnetlauncher.exe";
                        listToUpdate[i].Arguments = LaunchCodesList[y] + " -l -n"; // -n: notask -l: leave open -t <seconds>: time to wait for game
                        listToUpdate[i].Launcher = BelongsToLauncher.BattleNet;
                        listToUpdate[i].PathType = PathType.Path;
                    }
                }
            }
        }

        public static void OpenBattleNet()
        {
            BattleNetProcess = new Process();
            BattleNetProcess.StartInfo.FileName = Path;
            BattleNetProcess.Start();
        }

        // There's a setting which automatically closes battle.net when you launch a game so no need to close manually
        public static void CloseBattleNetMainWindow()
        {
            BattleNetProcess.CloseMainWindow();
        }
    }
}
