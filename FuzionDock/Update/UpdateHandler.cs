using Fuzion.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fuzion.Update
{
    /// <summary>
    /// Probably obsolete
    /// </summary>
    public class UpdateHandler
    {
        private const string updateURL = "https://tzar.dev/fuzion_update/update.xml";
        private static bool manualCheckIssued = false;
        public void Init()
        {
            //AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
        }

        // this may be obsolete because of autoupdater
        public static void MoveUpdatedSettings(string oldVersion = "", string newVersion = "", bool refresh = false)
        {
            try
            {
                if (refresh)
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    Settings.Default.Upgrade();
                    Settings.Default.Reload();
                    ConfigurationManager.RefreshSection("userSettings");
                } else
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    string oldVersionPath = config.FilePath;
                    string newVersionPath = config.FilePath.Replace(oldVersion, newVersion);

                    if (!File.Exists(newVersionPath) && File.Exists(oldVersionPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newVersionPath));
                        File.Copy(oldVersionPath, newVersionPath, true); //copy the settings and overwrite just in case

                        Settings.Default.Upgrade();
                        Settings.Default.Reload();
                        ConfigurationManager.RefreshSection("userSettings");
                    }
                }

               



            }
            catch (Exception)
            {

            }
        }

        //public static void CheckForUpdates(bool manual = false)
        //{
        //    try
        //    {
        //        if (manual)
        //            manualCheckIssued = true;

        //        AutoUpdater.RunUpdateAsAdmin = false;
        //        AutoUpdater.Start(updateURL);
        //    }
        //    catch (Exception)
        //    {

        //    }

        //}



        //private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        //{
        //    if (args != null)
        //    {
        //        if (args.IsUpdateAvailable)
        //        {
        //            DialogResult dialogResult;
        //            if (args.Mandatory.Value)
        //            {
        //                dialogResult =
        //                    MessageBox.Show(
        //                        $@"Version {args.CurrentVersion} is available. You are using version {args.InstalledVersion}. This is required update. Press Ok to begin updating the application.", @"Update Available",
        //                        MessageBoxButtons.OK,
        //                        MessageBoxIcon.Information);
        //            }
        //            else
        //            {
        //                dialogResult =
        //                    MessageBox.Show(
        //                        $@"There is new version {args.CurrentVersion} available. You are using version {
        //                                args.InstalledVersion
        //                            }. Do you want to update the application now?", @"Update Available",
        //                        MessageBoxButtons.YesNo,
        //                        MessageBoxIcon.Information);
        //            }

        //            // Uncomment the following line if you want to show standard update dialog instead.
        //            // AutoUpdater.ShowUpdateForm(args);

        //            if (dialogResult.Equals(DialogResult.Yes) || dialogResult.Equals(DialogResult.OK))
        //            {
        //                try
        //                {
        //                    if (AutoUpdater.DownloadUpdate(args))
        //                    {
        //                        Application.Exit();
        //                    }
        //                }
        //                catch (Exception exception)
        //                {
        //                    MessageBox.Show(exception.Message, exception.GetType().ToString(), MessageBoxButtons.OK,
        //                        MessageBoxIcon.Error);
        //                }
        //            }
        //        }
        //        else
        //        {
        //            if (manualCheckIssued)
        //            {
        //                MessageBox.Show(@"You're up to date!", @"No update available",
        //                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        //            }

        //        }
        //    }
        //    else
        //    {
        //        MessageBox.Show(
        //                @"There is a problem reaching update server please check your internet connection and try again later.",
        //                @"Update check failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }

        //    manualCheckIssued = false;
        //}
    }
}
