using Fuzion.Extensions;
using Fuzion.Programs;
using Fuzion.Properties;
using System.Collections.Generic;
using static Fuzion.Programs.Serialization;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.MainWindow;
using System.IO;
using System.Windows;

namespace Fuzion.SettingsManager
{
    class GeneralSettings
    {

        public static void LoadFromSettings()
        {

            // Load UWP Settings if UWP
            if (UniversalPlatform.Startup.IsUniversalPlatform)
            {
                UniversalPlatform.OnUpdate.LoadUWPSettings();
            }

            // Main Lists
            ProgramObjects = DeserializedList(TargetXMLFile.Programs);

            System.Console.WriteLine("Loaded programs list count: "+ProgramObjects.Count);
            GameObjects = DeserializedConvertedGamesList();
            System.Console.WriteLine("Loaded game list count: " + GameObjects.Count);

            UpgradeNewProgramValuesOnLoad();

            //throw new System.Exception();

            // Load current objects into grid
            AppWindow.CreateGrid();

            for (int i = 0; i < GameObjects.Count; i++)
            {
                AppWindow.AddGameToGrid(GameObjects[i]);
            }

        }

        public static void UpdateSettings()
        {
            // obsolete
            Settings.Default.InitialScanFinished = true;

            // Save UWP Settings if UWP
            if (UniversalPlatform.Startup.IsUniversalPlatform)
            {
                UniversalPlatform.OnUpdate.UpdateUWPSettings();
            }

            Settings.Default.Save();

            SerializeList(ProgramObjects);
            SerializeList(GameObjects);
            System.Console.WriteLine($"Settings Saved with {GameObjects.Count} games & {ProgramObjects.Count} programs.");
        }

        private static void UpgradeNewProgramValuesOnLoad()
        {
            for (int i = 0; i < ProgramObjects.Count; i++)
            {
                if (string.IsNullOrEmpty(ProgramObjects[i].DockName))
                    ProgramObjects[i].DockName = ProgramObjects[i].DisplayName;
            }

            for (int i = 0; i < GameObjects.Count; i++)
            {
                if (string.IsNullOrEmpty(GameObjects[i].DockName))
                    GameObjects[i].DockName = GameObjects[i].DisplayName;
            }
        }
    }
}
