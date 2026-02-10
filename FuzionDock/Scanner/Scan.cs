using Fuzion.Programs;
using Fuzion.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fuzion.LauncherSpecific;
using Fuzion.Extensions;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.Programs.Serialization;
using static Fuzion.SettingsManager.GeneralSettings;
using static Fuzion.Scanner.Rescan;
using static Fuzion.MainWindow;
using System.Windows.Documents;
using Fuzion.SettingsManager;

namespace Fuzion.Scanner
{
    // Notes
    // Scanner finds the following launchers:  Steam, Uplay, Origin, Battle.net
    // Epic games has specific lookup of reg key
    // GoG needs specific lookup of reg key

    internal static class Scan
    {
        public enum ScanType { Normal, EventBased, Rescan }
        public static bool ScanInProgress { get; set; }

        public static void DeepScan(ScanType scanType = ScanType.Normal)
        {
            if (!ScanInProgress)
            {
                // Clear the recently added game lists before scanning to indicate only new games
                RecentlyAddedGameNames.Clear();
                RecentlyAddedGames.Clear();

                ScanInProgress = true;

                if (scanType == ScanType.Normal)
                {
                    // Animate rectangle
                    AnimateLoadingRectangle(true, "deepscan");

                    // Registry
                    ProgramObjects = GetRegistryGames();

                    // Steam
                    if (Steam.Exists)
                    {
                        List<Program> steamGames = Steam.GetSteamGames();
                        Steam.SteamUpdateProgramObjects(steamGames, ProgramObjects);
                    }

                    // Epic
                    if (EpicGames.Exists)
                    {
                        List<Program> epicGames = EpicGames.GetEpicGames();
                        EpicGames.EpicUpdateProgramObjects(epicGames, ProgramObjects);
                    }

                    // Battle.net
                    if (BattleNet.Exists)
                    {
                        BattleNet.BattleNetUpdateProgramObjects(ProgramObjects);
                    }

                    // Uplay
                    if (Uplay.Exists)
                    {
                        List<Program> uplayGames = Uplay.GetUplayGames();
                        Uplay.UplayUpdateProgramObjects(uplayGames, ProgramObjects);
                    }

                    // UWP
                    List<Program> uwpGames = UWP.GetUWPGames();
                    UWP.UWPUpdateProgramObjects(uwpGames, ProgramObjects);

                    // Update Dock Name if it's empty
                    foreach (Program p in ProgramObjects)
                    {
                        if (p.DockName.Length == 0)
                            p.DockName = p.DisplayName;
                    }

                    // Update Originals before any changes are saved
                    UpdateProgramListOriginals(ProgramObjects);

                    // Remove Blacklisted programs/games from the list so they don't get added
                    RemoveBlacklistedPrograms(updatedProgramList);

                    // Sort and add to grid
                    SortGamesFromProgramsAndAddToGrid(ProgramObjects);

                    //// Local DB Update is after ^ all game operations - moved to AnimateLoadingRectangle
                    //LocalDatabase.UpdateDatabaseProgramsAsync();

                    UpdateSettings();

                    // Set scan finished in AnimateLoadingRectangle

                    // Stop animating rectangle
                    AnimateLoadingRectangle(false, "deepscan");

                }

                // Animate only if user issued
                if (scanType == ScanType.Rescan)
                {
                    AnimateLoadingRectangle(true, "rescan");

                    // Registry
                    updatedProgramList = GetRegistryGames();

                    // Steam
                    if (Steam.Exists)
                    {
                        List<Program> steamGames = Steam.GetSteamGames();
                        Steam.SteamUpdateProgramObjects(steamGames, updatedProgramList);
                    }

                    // Epic
                    if (EpicGames.Exists)
                    {
                        List<Program> epicGames = EpicGames.GetEpicGames();
                        EpicGames.EpicUpdateProgramObjects(epicGames, updatedProgramList);
                    }

                    // Battle.net
                    if (BattleNet.Exists)
                    {
                        BattleNet.BattleNetUpdateProgramObjects(updatedProgramList);
                    }

                    // Uplay
                    if (Uplay.Exists)
                    {
                        List<Program> uplayGames = Uplay.GetUplayGames();
                        Uplay.UplayUpdateProgramObjects(uplayGames, updatedProgramList);
                    }

                    // UWP
                    List<Program> uwpGames = UWP.GetUWPGames();
                    UWP.UWPUpdateProgramObjects(uwpGames, updatedProgramList);

                    // Update Dock Name if it's empty
                    foreach (Program p in ProgramObjects)
                    {
                        if (p.DockName.Length == 0)
                            p.DockName = p.DisplayName;
                    }

                    // Update Originals before any changes are saved
                    UpdateProgramListOriginals(updatedProgramList);

                    // Remove Blacklisted programs/games from the list so they don't get added
                    //if (!fullRescan) // now always removed because blacklist is user controlled from Settings
                    RemoveBlacklistedPrograms(updatedProgramList);

                    // Transfer IsGame from original programList, this will not work well with older version

                    for (int i = 0; i < updatedProgramList.Count; i++)
                    {
                        Program prog = ProgramObjects.FirstOrDefault(p => p.DisplayName == updatedProgramList[i].DisplayName);

                        if(prog != null)
                        {
                            updatedProgramList[i].IsGame = prog.IsGame;
                        }
                    }

                    Console.WriteLine("RESCAN FINISHED");

                    AnimateLoadingRectangle(false, "rescan");
                }

                // Set scan finished
                ScanInProgress = false;
                CheckGameObjectDBReadyness = true;
            }
        }

        private static void UpdateProgramListOriginals(List<Program> progList)
        {
            foreach (Program prog in progList)
            {
                prog.OriginalPath = prog.Path;
                prog.OriginalPathType = prog.OriginalPathType;
                prog.OriginalLauncher = prog.Launcher;
            }
        }

        private static void RemoveBlacklistedPrograms(List<Program> list)
        {
            List<string> blacklist = Blacklist.List;

            if (blacklist.Count > 0 && list.Count > 0)
            {
                List<Program> temp = list.Where(p => blacklist.Contains(p.DisplayName)).ToList();

                for (int i = 0; i < temp.Count; i++)
                {
                    list.Remove(temp[i]);
                    Console.WriteLine("Removing Blacklisted item from rescanned programs: " + temp[i].DisplayName);
                }
            }
        }

        public static List<Program> GetRegistryGames(bool dump = false)
        {
            List<Program> registryList = new List<Program>();
            List<Program> linqSafeRegistryList = new List<Program>();

            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            RegistryHive[] hiveKeys = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 };

            Parallel.ForEach(hiveKeys, hiveKey =>
            {
                foreach (RegistryView view in views)
                {
                    RegistryKey basekey = RegistryKey.OpenBaseKey(hiveKey, view);
                    RegistryKey regKey = basekey.OpenSubKey(uninstallKey);

                    if (basekey == null || regKey == null)
                    {
                        continue;
                    }

                    string[] subkeys = regKey.GetSubKeyNames();

                    //foreach (string subKeyName in subkeys)
                    for (int i = 0; i < subkeys.Length; i++)
                    {
                        RegistryKey subKey = regKey.OpenSubKey(subkeys[i]);

                        if (subKey == null)
                        {
                            continue;
                        }

                        string subKeyDisplayName = subKey.GetValue("DisplayName")?.ToString();

                        string subKeyDisplayIcon = subKey.GetValue("DisplayIcon")?.ToString();

                        string subKeyInstallLocation = subKey.GetValue("InstallLocation")?.ToString();

                        string subKeyUninstallString = subKey.GetValue("UninstallString")?.ToString();

                        string subkeyPublisher = subKey.GetValue("Publisher")?.ToString();

                        if (dump)
                        {
                            Console.WriteLine("<<<Registry Key Definition Start>>>");
                            Console.WriteLine("DisplayName: " + subKeyDisplayName);
                            Console.WriteLine("Icon: " + subKeyDisplayIcon);
                            Console.WriteLine("InstallLocation: " + subKeyInstallLocation);
                            Console.WriteLine("Uninstall: " + subKeyUninstallString);
                            Console.WriteLine("Publisher: " + subkeyPublisher);
                            Console.WriteLine("Registry View: " + view.ToString());
                            Console.WriteLine("Location: " + subKey.ToString());
                            Console.WriteLine("<<<Registry Key Definition End>>>");
                        }


                        if (subKeyDisplayName == null || subKeyInstallLocation == null)
                        {
                            continue;
                        }

                        // If Programs List doesn't contain this name and the path exists, add it
                        if (Directory.Exists(subKeyInstallLocation) /*&& !registryList.Any(prog => prog.DisplayName == subKeyDisplayName)*/)
                        {
                            Program newProgram = new Program
                            {
                                DisplayName = subKeyDisplayName,
                                Path = subKeyInstallLocation,
                                OriginalPath = subKeyInstallLocation,
                                PathType = PathType.Path,
                                OriginalPathType = PathType.Path,
                                UninstallPath = subKeyUninstallString,
                                WorkDir = subKeyInstallLocation,
                                SystemIcon = subKeyDisplayIcon,
                                Publisher = subkeyPublisher
                            };

                            if (!string.IsNullOrEmpty(subkeyPublisher) && subkeyPublisher.ToUpperInvariant() == "ELECTRONIC ARTS")
                            {
                                newProgram.Launcher = BelongsToLauncher.Origin;
                                newProgram.OriginalLauncher = BelongsToLauncher.Origin;
                            }


                            if (!string.IsNullOrEmpty(subkeyPublisher) && subkeyPublisher.ToUpperInvariant() == "BLIZZARD ENTERTAINMENT")
                            {
                                newProgram.Launcher = BelongsToLauncher.BattleNet;
                                newProgram.OriginalLauncher = BelongsToLauncher.BattleNet;
                            }

                            // Add to the linqSafe list and sort later
                            linqSafeRegistryList.Add(newProgram);
                        }
                    }
                }
            });

            Console.WriteLine("Linq Safe List count: "+linqSafeRegistryList.Count);
            // Sort the linqSafeRegistryCollection and remove any null entries
            registryList = linqSafeRegistryList.Where(p => p != null && !string.IsNullOrEmpty(p.DisplayName))
                                               .GroupBy(p => p.DisplayName)
                                               .Select(name => name.First())
                                               .ToList();
            Console.WriteLine("Registry List count (after removing duplicates): " + registryList.Count);

            Console.WriteLine("Registry Scan Completed");

            return registryList;
        }

        #region Obsolete code - limit requests by using regex to exclude GUID based names except EA publisher games
        // Limit to non-guid keys
        //if (!Regex.IsMatch(subKeyName, @"\{(.*?)\}"))
        //{
        //    NON GUID KEY SCAN goes here
        //}
        //else //if the key name is between curly brackets {}
        //{
        //    // ORIGIN specific
        //    try
        //    {
        //        RegistryKey subKey = regKey.OpenSubKey(subKeyName);
        //        if (subKey == null) continue;

        //        object subKeyPublisher = subKey.GetValue("Publisher");

        //        if (subKeyPublisher.ToString().ToUpperInvariant().Contains("ELECTRONIC ARTS"))
        //        {
        //            object subKeyDisplayName = subKey.GetValue("DisplayName");

        //            object subKeyDisplayIcon = subKey.GetValue("DisplayIcon");

        //            object subKeyInstallLocation = subKey.GetValue("InstallLocation");

        //            object subKeyUninstallString = subKey.GetValue("UninstallString");

        //            if (subKeyInstallLocation == null)
        //            {
        //                subKeyInstallLocation = subKey.GetValue("UninstallString");
        //                if (subKeyInstallLocation == null) continue;
        //                FileInfo fi = new FileInfo(subKeyInstallLocation.ToString());
        //                subKeyInstallLocation = fi.Directory.FullName;
        //            }

        //            if (subKeyDisplayName == null || subKeyInstallLocation == null) continue;

        //            string thisname = subKeyDisplayName.ToString();
        //            string thispath = subKeyInstallLocation.ToString();

        //            //Add to programs list for easy indexing
        //            if (Directory.Exists(thispath) && !registryList.Any(prog => prog.DisplayName == thisname))
        //            {
        //                GetMetaIfSpecialProgram(thisname, thispath);

        //                Program newProgram = new Program
        //                {
        //                    DisplayName = thisname,
        //                    Path = thispath,
        //                    UninstallPath = subKeyUninstallString.ToString(),
        //                    WorkDir = thispath,
        //                    SystemIcon = subKeyDisplayIcon.ToString(),
        //                    Launcher = BelongsToLauncher.Origin
        //                };

        //                registryList.Add(newProgram);
        //            }
        //        }

        //        continue;
        //    }
        //    catch (Exception)
        //    {

        //    }
        //}

        #endregion

        public static string GetEpicGamesInstallLocationFromRegistry()
        {
            string result = string.Empty;
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            RegistryHive[] keys = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 };

            Parallel.ForEach(keys, hive =>
            {
                foreach (var view in views)
                {
                    RegistryKey regKey = null,
                        basekey = null;

                    try
                    {
                        basekey = RegistryKey.OpenBaseKey(hive, view);
                        regKey = basekey.OpenSubKey(uninstallKey);
                    }
                    catch (Exception) { continue; }

                    if (basekey == null || regKey == null)
                        continue;

                    foreach (string subKeyName in regKey.GetSubKeyNames())
                    {
                        if(Regex.IsMatch(subKeyName, @"\{(.*?)\}"))
                        {
                            try
                            {
                                RegistryKey subKey = regKey.OpenSubKey(subKeyName);
                                if (subKey == null) continue;

                                string subKeyDisplayName = subKey.GetValue("DisplayName")?.ToString();
                                string subkeyInstallLocation = subKey.GetValue("InstallLocation")?.ToString();

                                if (!string.IsNullOrEmpty(subKeyDisplayName)
                                    && !string.IsNullOrEmpty(subkeyInstallLocation)
                                    && subKeyDisplayName == "Epic Games Launcher"
                                    && Directory.Exists(subkeyInstallLocation))
                                {
                                    Console.WriteLine("Epic Games Registry Location: "+subKey + " " + subKeyName);
                                    result = subkeyInstallLocation;
                                }

                                continue;
                            }
                            catch (Exception)
                            {

                            }
                        }
                    }
                }
            });

            return result;
        }

        public static string GetSteamDetailsFromRegistry()
        {
            Console.WriteLine("Looking for steam in registry");

            string result = string.Empty;
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

            RegistryHive[] keys = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 };

            Parallel.ForEach(keys, hive =>
            {
                foreach (var view in views)
                { 
                    RegistryKey basekey = RegistryKey.OpenBaseKey(hive, view);
                    RegistryKey regKey = basekey.OpenSubKey(uninstallKey);


                    if (basekey == null || regKey == null)
                    {
                        continue;
                    }

                    string[] subkeys = regKey.GetSubKeyNames();

                    foreach (string subKeyName in subkeys)
                    {
                        
                        //if (Regex.IsMatch(subKeyName, @"\{(.*?)\}"))
                        if (subKeyName == "Steam")
                        {
                            //try
                            Console.WriteLine("Opening subkey: "+ subKeyName);
                            RegistryKey subKey = regKey.OpenSubKey(subKeyName);

                                if (subKey == null)
                                {
                                Console.WriteLine("Subkey was null, skipping");
                                continue;
                                }

                            Console.WriteLine("Subkey was NOT null");

                            string subKeyDisplayName = subKey.GetValue("DisplayName")?.ToString();
                                string subkeyInstallLocation = subKey.GetValue("InstallLocation")?.ToString();

                            Console.WriteLine("Subkey details: "+subKeyDisplayName + " "+subkeyInstallLocation);

                            if (!string.IsNullOrEmpty(subKeyDisplayName)
                                    && !string.IsNullOrEmpty(subkeyInstallLocation)
                                    && subKeyDisplayName.ToUpperInvariant() == "STEAM"
                                    && Directory.Exists(subkeyInstallLocation))
                                {
                                    Console.WriteLine("Steam Registry Location: " + subKey + " " + subKeyName);
                                    result = subkeyInstallLocation;
                                }
                            //}
                            //catch (Exception)
                            //{

                            //}
                        }
                    }
                }
            });

            return result;
        }
    }
}
