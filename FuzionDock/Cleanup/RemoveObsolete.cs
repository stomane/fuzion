using Fuzion.Programs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Fuzion.Programs.ProgramManager;

namespace Fuzion.Cleanup
{
    static class RemoveObsolete
    {
        public static void CleanEverythingParallel()
        {
            CleanGameIconsParallel();
            CleanTemporaryFolder();
            CleanCroppedFolder();
        }

        public static void CleanGameIconsParallel()
        {
            string[] iconsFolder = Directory.GetFiles(Fuzion.MainWindow.DefaultAssetPath + @"Icons\");

            Parallel.ForEach(iconsFolder, (icon, state, index) =>
            {
                if(!GameObjects.Any(g => g.Icon == icon))
                {
                    File.Delete(icon);
                }
            });

            string[] originalIconsFolder = Directory.GetFiles(Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\");

            Parallel.ForEach(originalIconsFolder, (icon, state, index) =>
            {
                if (!GameObjects.Any(g => g.OriginalIcon == icon))
                {
                    File.Delete(icon);
                }
            });
        }

        public static void CleanTemporaryFolder()
        {
            try
            {
                Directory.Delete(Fuzion.MainWindow.DefaultAssetPath + @"temp\", true);
            }
            catch (Exception)
            {

            }

        }

        public static void CleanCroppedFolder()
        {
            try
            {
                Directory.Delete(Fuzion.MainWindow.DefaultAssetPath + @"cropped\", true);
            }
            catch (Exception)
            {

            }
         
        }

        public static void CleanTemporaryFolder(string path)
        {
            Directory.Delete(path, true);
        }
    }
}
