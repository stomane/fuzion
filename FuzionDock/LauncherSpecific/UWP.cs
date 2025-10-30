using Fuzion.Programs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.ApplicationModel;
using System.Security.Principal;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using Windows.System;

namespace Fuzion.LauncherSpecific
{
    class UWP
    {
        public static List<Program> GetUWPGames()
        {
            List<Program> result = new List<Program>();

            PackageManager manager = new PackageManager();
            IEnumerable<Package> packages = manager.FindPackagesForUser(WindowsIdentity.GetCurrent().User.Value);
            foreach (var package in packages)
            {
                if (package.IsFramework || package.IsResourcePackage || package.SignatureKind != PackageSignatureKind.Store)
                {
                    continue;
                }

                try
                {
                    if (package.InstalledLocation == null)
                    {
                        continue;
                    }
                }
                catch
                {
                    // InstalledLocation accessor may throw Win32 exception for unknown reason
                    continue;
                }

                try
                {
                    string manifestPath;
                    if (package.IsBundle)
                    {
                        manifestPath = @"AppxMetadata\AppxBundleManifest.xml";
                    }
                    else
                    {
                        manifestPath = "AppxManifest.xml";
                    }

                    manifestPath = Path.Combine(package.InstalledLocation.Path, manifestPath);
                    var manifest = new XmlDocument();
                    manifest.Load(manifestPath);

                    var apxApp = manifest.SelectSingleNode(@"/*[local-name() = 'Package']/*[local-name() = 'Applications']//*[local-name() = 'Application'][1]");
                    var appId = apxApp.Attributes["Id"].Value;

                    var visuals = apxApp.SelectSingleNode(@"//*[local-name() = 'VisualElements']");
                    var iconPath = visuals.Attributes["Square150x150Logo"]?.Value;
                    if (string.IsNullOrEmpty(iconPath))
                    {
                        iconPath = visuals.Attributes["Square70x70Logo"]?.Value;
                        if (string.IsNullOrEmpty(iconPath))
                        {
                            iconPath = visuals.Attributes["Square44x44Logo"]?.Value;
                            if (string.IsNullOrEmpty(iconPath))
                            {
                                iconPath = visuals.Attributes["Logo"]?.Value;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(iconPath))
                    {
                        iconPath = Path.Combine(package.InstalledLocation.Path, iconPath);
                        iconPath = GetUWPGameIcon(iconPath);
                    }

                    var name = manifest.SelectSingleNode(@"/*[local-name() = 'Package']/*[local-name() = 'Properties']/*[local-name() = 'DisplayName']").InnerText;
                    if (name.StartsWith("ms-resource"))
                    {
                        name = GetIndirectResourceString(package.Id.FullName, package.Id.Name, name);
                        if (string.IsNullOrEmpty(name))
                        {
                            name = manifest.SelectSingleNode(@"/*[local-name() = 'Package']/*[local-name() = 'Identity']").Attributes["Name"].Value;
                        }
                    }

                    var app = new Program() // add all details here
                    {
                        DisplayName = name,
                        WorkDir = package.InstalledLocation.Path,
                        Path = "explorer.exe",
                        Arguments = $"shell:AppsFolder\\{package.Id.FamilyName}!{appId}",
                        // System icon will be disabled for now as it doesn't yield proper results
                        //SystemIcon = iconPath,
                        UWPAppID = package.Id.FamilyName,
                        Launcher = BelongsToLauncher.UWP
                    };

                    result.Add(app);
                }
                catch (Exception)
                {
                    //logger.Error(e, $"Failed to parse UWP game info.");
                }
            }

            return result;
        }

        public static void UWPUpdateProgramObjects(List<Program> uwpProgramList, List<Program> listToUpdate)
        {
            foreach (Program program in uwpProgramList)
            {
                if (!listToUpdate.Contains(program))
                    listToUpdate.Add(program);
            }
        }

        private static string GetUWPGameIcon(string defPath)
        {
            if (File.Exists(defPath))
            {
                return defPath;
            }

            var folder = Path.GetDirectoryName(defPath);
            var fileMask = Path.GetFileNameWithoutExtension(defPath) + ".scale*.png";
            var files = Directory.GetFiles(folder, fileMask);

            if (files == null || files.Count() == 0)
            {
                return string.Empty;
            }
            else
            {
                var icons = files.Where(a => Regex.IsMatch(a, @"\.scale-\d+\.png"));
                if (icons.Any())
                {
                    return icons.OrderBy(a => a).Last();
                }

                return string.Empty;
            }
        }

        public static string GetIndirectResourceString(string fullName, string packageName, string resource)
        {
            var resUri = new Uri(resource);
            var resourceString = string.Empty;
            if (resource.StartsWith("ms-resource://"))
            {
                resourceString = $"@{{{fullName}? {resource}}}";
            }
            else if (resource.Contains('/'))
            {
                resourceString = $"@{{{fullName}? ms-resource://{packageName}/{resource.Replace("ms-resource:", "").Trim('/')}}}";
            }
            else
            {
                resourceString = $"@{{{fullName}? ms-resource://{packageName}/resources/{resUri.Segments.Last()}}}";
            }

            var sb = new StringBuilder(1024);
            var result = Fuzion.Native.NativeMethods.SHLoadIndirectString(resourceString, sb, sb.Capacity, IntPtr.Zero);
            if (result == 0)
            {
                return sb.ToString();
            }

            resourceString = $"@{{{fullName}? ms-resource://{packageName}/{resUri.Segments.Last()}}}";
            result = Fuzion.Native.NativeMethods.SHLoadIndirectString(resourceString, sb, sb.Capacity, IntPtr.Zero);
            if (result == 0)
            {
                return sb.ToString();
            }

            return string.Empty;
        }

        public static void LaunchUWPGame()
        {
            //Launcher.LaunchUriAsync()
        }
    }
}
