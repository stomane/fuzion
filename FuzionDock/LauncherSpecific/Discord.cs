using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.LauncherSpecific
{
    class Discord
    {
        public static bool Exists = DiscordExists();
        public static string Path;
        public static string Uninstall;
        public const string Arguments = "--processStart Discord.exe";
        private const string RegistryLocation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Discord";

        public static bool DiscordExists()
        {
            // new method which uses 32bit registry
            try
            {
                // Opens the registry in 32bit mode since in 64bits battle.net uninstall entry is under Wow6432Node Key
                using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
                {
                    // goes to the uninstall entry on the battle.net client and retrieves the InstallLocation key to get the path
                    using (RegistryKey discordKey = registry.OpenSubKey(RegistryLocation))
                    {
                        if(discordKey != null)
                        {
                            Path = System.IO.Path.Combine(discordKey.GetValue("InstallLocation").ToString(), "Update.exe");
                            Uninstall = discordKey.GetValue("UninstallString").ToString();
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
    }
}
