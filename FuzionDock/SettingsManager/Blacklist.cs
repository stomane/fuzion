using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.SettingsManager
{
    static class Blacklist
    {
        private static string FilePath { get; } = GetFilePath();
        private static string FolderPath { get; } = GetFolderPath();

        public static List<string> List { get; private set; } = Load();

        private static string GetFilePath()
        {
            return Path.Combine(MainWindow.DefaultAssetPath, @"db\", @"blacklist.fzn");
        }

        private static string GetFolderPath()
        {
            return Path.Combine(MainWindow.DefaultAssetPath, @"db\");
        }

        /// <summary>
        /// Get the Blacklist as a list of strings
        /// </summary>
        /// <returns></returns>
        public static List<string> Get()
        {
            return List;
        }

        private static void Save()
        {
            File.WriteAllLines(FilePath, List);
        }

        public static List<string> Load()
        {
            var res = new List<string>();

            Directory.CreateDirectory(FolderPath);

            if (File.Exists(FilePath))
            {
                res = File.ReadAllLines(FilePath).ToList();
            } else
            {
                // Create empty
                var sw = File.Create(FilePath);
                sw.Dispose();
            }

            return res;
        }

        public static void ReloadList()
        {
            List = Load();
        }

        public static void Add(string str)
        {
            if (str.Length > 0 && !List.Contains(str))
            {
                List.Add(str);
                Save();
            }
        }

        /// <summary>
        /// Returns true if an item was successfully removed
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool Remove(string str)
        {
            if (str.Length > 0 && List.Contains(str))
            {
                List.Remove(str);
                Save();
                return true;
            }

            return false;
        }

        public static void Clear()
        {
            List.Clear();
            Save();
        }
    }
}
