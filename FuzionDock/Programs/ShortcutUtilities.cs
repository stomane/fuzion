using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Programs
{
    public static class ShortcutUtilities
    {
        public static List<string> GetTargetPath(string filePath)
        {
            List<string> targetPath = ResolveMsiShortcut(filePath);
            if (targetPath == null)
            {
                Console.WriteLine("Target is not MSI Shortcut");
                
                targetPath = ResolveShortcutPath(filePath);
            }

            //Console.WriteLine("Target path is null or empty: "+string.IsNullOrEmpty(targetPath[0]));

            if (string.IsNullOrEmpty(targetPath[0]))
            {
                //Console.WriteLine("target path was null or empty, check if it's UWP for name: "+Path.GetFileNameWithoutExtension(filePath));
                var uwpapps = LauncherSpecific.UWP.GetUWPGames();
                var exeName = Path.GetFileNameWithoutExtension(filePath);

                for (int i = 0; i < uwpapps.Count; i++)
                {
                    if (uwpapps[i].DisplayName.Contains(exeName) || exeName.Contains(uwpapps[i].DisplayName))
                    {
                        Console.WriteLine("Found UWP app: "+ uwpapps[i].DisplayName);
                        targetPath = new List<string> { "explorer.exe", uwpapps[i].Arguments };
                    }
                }
            }

            return targetPath;
        }

        public static string GetInternetShortcut(string filePath)
        {
            string url = "";

            using (TextReader reader = new StreamReader(filePath))
            {
                string line = "";
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        //url = line.Remove(0, 4);
                        //break;

                        string[] splitLine = line.Split('=');
                        if (splitLine.Length > 0)
                        {
                            url = splitLine[1];
                            break;
                        }
                    }
                }
            }

            return url;
        }

        static List<string> ResolveShortcutPath(string filePath)
        {
            // IWshRuntimeLibrary is in the COM library "Windows Script Host Object Model"
            IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();

            try
            {
                IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(filePath);
                List<string> result = new List<string> { shortcut.TargetPath, shortcut.Arguments };
                return result;
            }
            catch (COMException)
            {
                // A COMException is thrown if the file is not a valid shortcut (.lnk) file 
                return null;
            }
        }

        static List<string> ResolveMsiShortcut(string file)
        {
            StringBuilder product = new StringBuilder(NativeMethods.MaxGuidLength + 1);
            StringBuilder feature = new StringBuilder(NativeMethods.MaxFeatureLength + 1);
            StringBuilder component = new StringBuilder(NativeMethods.MaxGuidLength + 1);

            _ = NativeMethods.MsiGetShortcutTarget(file, product, feature, component);

            int pathLength = NativeMethods.MaxPathLength;
            StringBuilder path = new StringBuilder(pathLength);

            NativeMethods.InstallState installState = NativeMethods.MsiGetComponentPath(product.ToString(), component.ToString(), path, ref pathLength);
            if (installState == NativeMethods.InstallState.Local)
            {
                List<string> resultList = new List<string>();
                resultList.Add(path.ToString());
                resultList.Add("");
                return resultList;
            }
            else
            {
                return null;
            }
        }

        private class NativeMethods
        {
            [DllImport("msi.dll", CharSet = CharSet.Unicode)]
            public static extern uint MsiGetShortcutTarget(string targetFile, StringBuilder productCode, StringBuilder featureID, StringBuilder componentCode);

            [DllImport("msi.dll", CharSet = CharSet.Unicode)]
            public static extern InstallState MsiGetComponentPath(string productCode, string componentCode, StringBuilder componentPath, ref int componentPathBufferSize);

            public const int MaxFeatureLength = 38;
            public const int MaxGuidLength = 38;
            public const int MaxPathLength = 1024;

            public enum InstallState
            {
                NotUsed = -7,
                BadConfig = -6,
                Incomplete = -5,
                SourceAbsent = -4,
                MoreData = -3,
                InvalidArg = -2,
                Unknown = -1,
                Broken = 0,
                Advertised = 1,
                Removed = 1,
                Absent = 2,
                Local = 3,
                Source = 4,
                Default = 5
            }
        }
    }
}
