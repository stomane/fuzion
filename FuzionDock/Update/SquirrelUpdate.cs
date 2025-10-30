using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Fuzion.WindowsManager;
using Org.BouncyCastle.Crypto.Generators;
using Squirrel;

namespace Fuzion.Update
{
    class SquirrelUpdate
    {
        public static bool devUpdateEnabled = false;
        static string UpdateURL = @"https://fuzion.gg/update";
        static bool updateLoaded;

        public static void WriteLastConfigFilePath()
        {
            Console.WriteLine(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
        }

        /// <summary>
        /// Make a backup of our settings.
        /// Used to persist settings across updates.
        /// </summary>
        public static void BackupSettings()
        {
            string settingsFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            string destination = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
            File.Copy(settingsFile, destination, true);
        }

        /// <summary>
        /// Restore our settings backup if any.
        /// Used to persist settings across updates.
        /// </summary>
        public static void RestoreSettings()
        {
            //Restore settings after application update            
            string destFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
            string sourceFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
            // Check if we have settings that we need to restore
            if (!File.Exists(sourceFile))
            {
                // Nothing we need to do
                return;
            }
            // Create directory as needed
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            }
            catch (Exception) { }

            // Copy our backup file in place 
            try
            {
                File.Copy(sourceFile, destFile, true);
            }
            catch (Exception) { }

            // Delete backup file
            try
            {
                File.Delete(sourceFile);
            }
            catch (Exception) { }

        }

        public static async void UpdateButton()
        {
            if (devUpdateEnabled)
            {
                WindowsManager.OpenWindow.Notification("WARN: DEV UPDATE IS ENABLED");
                UpdateURL = @"https://fuzion.gg/fuzion_update_dev";
            }

            if (updateLoaded == false)
            {
                OpenWindow.NotificationToast("", "Checking for updates", 220, 55);
                try
                {
                    using (var mgr = new UpdateManager(UpdateURL, "Fuzion"))
                    {
                        var updateInfo = await mgr.CheckForUpdate().ConfigureAwait(false);
                        Console.WriteLine("Available updates count: " + updateInfo.ReleasesToApply.Count);

                        if (updateInfo.ReleasesToApply.Count > 0)
                        {
                            Console.WriteLine("Updating...");
                            await mgr.UpdateApp().ConfigureAwait(false);
                            BackupSettings();
                            updateLoaded = true;
                            var result = OpenWindow.Notification($"Yay, there's an update! It will be applied when you restart Fuzion. Would you like to do it now?", "Update available", OpenWindow.NotificationWindowType.YesNo);

                            if (result == OpenWindow.NotificationResult.Yes)
                            {
                                MainWindow.RestartFuzion();
                            }
                        }
                        else
                        {
                            OpenWindow.NotificationToast("", "You're up to date!", 220d, 55d);
                        }
                    }
                }
                catch (Exception)
                {
                    OpenWindow.NotificationToast("Update check failed, try again later", "Oops...", 230, 200);
                }
            }
            else
            {
                var result = OpenWindow.Notification($"Yay, there's an update! It will be applied when you restart Fuzion. Would you like to do it now?", "Update available", OpenWindow.NotificationWindowType.YesNo);

                if (result == OpenWindow.NotificationResult.Yes)
                {
                    MainWindow.RestartFuzion();
                }
            }

        }

        public static async void Update()
        {
            if (devUpdateEnabled)
            {
                WindowsManager.OpenWindow.Notification("WARN: DEV UPDATE IS ENABLED");
                UpdateURL = @"https://fuzion.gg/fuzion_update_dev";
            }

            if(updateLoaded == false)
            {
                try
                {
                    using (UpdateManager mgr = new UpdateManager(UpdateURL, "Fuzion"))
                    {
                        var updateInfo = await mgr.CheckForUpdate().ConfigureAwait(false);
                        Console.WriteLine("Available updates count: " + updateInfo.ReleasesToApply.Count);

                        if (updateInfo.ReleasesToApply.Count > 0)
                        {
                            Console.WriteLine("Updating...");
                            await mgr.UpdateApp().ConfigureAwait(false);
                            BackupSettings();
                            updateLoaded = true;

                            OpenWindow.NotificationToast("Fuzion has been updated! Restart it to run the new version", "Update Pending", 225d, 100d, "", 7d);
                        }
                    }
                }
                catch (Exception)
                {
                    //OpenWindow.NotificationToast("", "Update check failed", 220d,55d);
                }
            }
        }


    }
}
