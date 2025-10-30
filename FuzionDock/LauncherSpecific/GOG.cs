using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.LauncherSpecific
{
    static class GOG
    {
        public static bool Exists = GOGExists();
        public static bool ShadowLaunchEnabled { get; set; }
        public static string Path;
        public const string Arguments = @"/launchViaAutoStart";
        public static string WorkDir;
        private const string RegistryKeyLocation = @"SOFTWARE\GOG.com\GalaxyClient\paths";
        public const string ExeName = @"GalaxyClient.exe";
        private const string GamesRegistryKeyLocation = @"SOFTWARE\GOG.com\Games";
        public const string ClientProcessName = "GalaxyClient";

        private static bool GOGExists()
        {
            bool result = false;

            try
            {
                // Opens the registry in 32bit mode since in 64bits battle.net uninstall entry is under Wow6432Node Key
                using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    // goes to the uninstall entry on the battle.net client and retrieves the InstallLocation key to get the path
                    using (RegistryKey gogKey = registry.OpenSubKey(RegistryKeyLocation))
                    {
                        if(gogKey != null)
                        {
                            WorkDir = gogKey.GetValue("client").ToString();
                            Path = System.IO.Path.Combine(WorkDir, ExeName);
                            result = true;
                        }
                    }
                }
            }
            catch (Exception)
            {

            }

            return result;
        }

        // GoG doesn't need to run to play games so this is unnecessary
        public static void CloseGOG()
        {
            Process[] processList = Process.GetProcessesByName("GalaxyClient");

            foreach (var item in processList)
            {
                Console.WriteLine("Process for steam found: " + item.ProcessName);
            }
            if (processList.Length != 0)
            {
                processList[0].CloseMainWindow();
            }
        }
    }
}
