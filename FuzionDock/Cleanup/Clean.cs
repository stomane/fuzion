using Fuzion.WindowsManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static Fuzion.MainWindow;

namespace Fuzion.Cleanup
{
    static class Clean
    {
        public enum DataStore { DockContent, Database, Settings, All }

        public static void CustomData(DataStore ds)
        {
            if(ds == DataStore.DockContent)
            {
                DockContent();
            }

            if (ds == DataStore.Database)
            {
                LocalDatabase();
            }

            if (ds == DataStore.Settings)
            {
                Settings();
            }

            if (ds == DataStore.All)
            {
                All();
            }
        }

        private static void All()
        {
            try
            {
                LocalDatabase();
                Settings();
                DockContent();

                OpenWindow.Notification("Reset completed successfully! Fuzion will now attempt to restart.");

                RestartFuzion();
            }
            catch (Exception)
            {
                OpenWindow.Notification("Reset Failed. You can try and manually remove the following directories:\n" + DefaultAssetPath + "\n" + DefaultSettingsPath);
            }
        }

        private static void LocalDatabase()
        {
            try
            {
                string dbPath = Path.Combine(DefaultAssetPath, "db");

                if (Directory.Exists(dbPath))
                {
                    string gamesFilePath = Path.Combine(dbPath, "games.fzn");
                    string programsFilePath = Path.Combine(dbPath, "programs.fzn");

                    if (File.Exists(gamesFilePath))
                    {
                        File.Delete(gamesFilePath);
                    }

                    if (File.Exists(programsFilePath))
                    {
                        File.Delete(programsFilePath);
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        private static void Settings()
        {
            try
            {
                if (Directory.Exists(DefaultSettingsPath))
                {
                    Directory.Delete(DefaultSettingsPath, true);
                }
            }
            catch (Exception)
            {

            }
        }

        private static void DockContent()
        {
            try
            {
                string path = Path.Combine(DefaultAssetPath, "programs");
                string gamesPath = Path.Combine(path, "games.xml");
                string programsPath = Path.Combine(path, "programs.xml");

                if (Directory.Exists(path))
                {
                    if (File.Exists(gamesPath))
                    {
                        File.Delete(gamesPath);
                    }

                    if (File.Exists(programsPath))
                    {
                        File.Delete(programsPath);
                    }
                }
            }
            catch (Exception)
            {

            }
        }
    }
}
