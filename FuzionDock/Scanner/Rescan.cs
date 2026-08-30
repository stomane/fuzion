using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fuzion.Programs;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.Scanner.Scan;
using static Fuzion.MainWindow;
using System.Windows;
using static Fuzion.SettingsManager.GeneralSettings;
using System.Windows.Threading;

namespace Fuzion.Scanner
{
    class Rescan
    {
        public static List<Program> updatedProgramList = new List<Program>();
        private static List<Program> missingProgramsList = new List<Program>();
        private static List<Program> addedProgramsList = new List<Program>();

        private static readonly List<Game> removeTheseGamesList = new List<Game>();

        private static readonly DispatcherTimer removeDispatcher = AnimatedRemoveDispatcherInit();
        //private static readonly DispatcherTimer addDispatcher = AnimatedAddDispatcherInit();

        private static bool removeActive = false;
        //private static bool addActive = false;
        //public static bool scanning;

        // Guards the whole rescan pipeline (registry scan + Gemini/IGDB classification + grid
        // update), unlike Scan.ScanInProgress which DeepScan clears as soon as its own
        // registry-scan phase finishes - long before classification is done.
        private static bool rescanPipelineActive = false;

        private static int index = 0;

        //private static DispatcherTimer AnimatedAddDispatcherInit()
        //{
        //    DispatcherTimer disp = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(gridAnimationLength) };
        //    disp.Tick += AddActions_Tick;
        //    return disp;
        //}

        private static DispatcherTimer AnimatedRemoveDispatcherInit()
        {
            DispatcherTimer disp = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(gridAnimationLength) };
            disp.Tick += RemoveActions_Tick;
            return disp;
        }

        //private static void AddActions_Tick(object sender, EventArgs e)
        //{
        //    CheckIsGameAndAddToGrid(addedProgramsList[index]);
        //    index++;

        //    if (index == addedProgramsList.Count)
        //    {
        //        index = 0;
        //        addActive = false;
        //        DispatcherTimer timer = sender as DispatcherTimer;
        //        timer.Stop();
        //    }
        //}

        private static void RemoveActions_Tick(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                removeTheseGamesList[index].Remove();
            }));
            index++;

            if (index >= removeTheseGamesList.Count)
            {
                index = 0;
                removeActive = false;
                DispatcherTimer timer = sender as DispatcherTimer;
                timer.Stop();
                ScanInProgress = false;
            }
        }

        public static async void UpdatePrograms()
        {
            // Blocks a second rescan from starting while classification (Gemini/IGDB) is
            // still running - Scan.ScanInProgress alone isn't enough since DeepScan clears
            // it right after the registry-scan phase, before classification even starts.
            if (rescanPipelineActive)
            {
                return;
            }

            rescanPipelineActive = true;
            AnimateLoadingRectangle(true, "rescan-pipeline");

            try
            {
                await Task.Run(() => DeepScan(ScanType.Rescan)).ConfigureAwait(false);

                missingProgramsList = GetMissingPrograms();
                addedProgramsList = GetAddedPrograms();

                // Add recently installed games
                InstantlyAddNewGames();
                // Remove recently uninstalled games
                InstantlyRemoveMissingGames();

                // update the programObjectsList after all games have been updated
                ProgramObjects = updatedProgramList.ToList();

                UpdateSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rescan.UpdatePrograms failed: {ex}");
                StopAnimatingLoadingRectangle();
                ScanInProgress = false;
            }
            finally
            {
                AnimateLoadingRectangle(false, "rescan-pipeline");
                rescanPipelineActive = false;
            }
        }

        //instant remove all method
        private static void InstantlyRemoveMissingGames()
        {
            // Get new missing programs
            if (missingProgramsList.Count > 0)
            {
                removeTheseGamesList.Clear();

                for (int x = 0; x < missingProgramsList.Count; x++)
                {
                    for (int y = 0; y < GameObjects.Count; y++)
                    {
                        if (GameObjects[y].DisplayName == missingProgramsList[x].DisplayName)
                        {
                            if (GameObjects[y].IsManuallyAdded)
                            {
                                if (!System.IO.Directory.Exists(GameObjects[y].WorkDir))
                                {
                                    removeTheseGamesList.Add(GameObjects[y]);
                                }
                            } else
                            {
                                removeTheseGamesList.Add(GameObjects[y]);
                            }
                        }
                    }
                }

                // Check if something was missed last time
                for (int x = 0; x < GameObjects.Count; x++)
                {
                    if (!updatedProgramList.Any(p => p.DisplayName == GameObjects[x].DisplayName))
                    {
                        if (GameObjects[x].IsManuallyAdded)
                        {
                            if (!System.IO.Directory.Exists(GameObjects[x].WorkDir) || !System.IO.File.Exists(GameObjects[x].Path))
                            {
                                removeTheseGamesList.Add(GameObjects[x]);
                            }
                        }
                        else
                        {
                            removeTheseGamesList.Add(GameObjects[x]);
                        }
                    }
                }

                if (removeTheseGamesList.Count > 0)
                {
                    //Animate remove
                    removeDispatcher.Start();

                    ////Instantly remove
                    //for (int i = 0; i < removeTheseGames.Count; i++)
                    //{
                    //    Application.Current.Dispatcher.Invoke(new Action(() =>
                    //    {
                    //        removeTheseGames[i].Remove();
                    //    }));
                    //}
                }
            }
        }

        //instant add all method
        private static void InstantlyAddNewGames(bool fullRescan = false)
        {
            //if (fullRescan)
            //{
                // Add every game missing from the dock
                for (int x = 0; x < updatedProgramList.Count; x++)
                {
                    //if (updatedProgramList[x].IsGame)
                    //if (IGDB.GameCheck.IsGame(updatedProgramList[x]))
                    //{
                    // If gameobjects doesnt contain the name of the program marked as game
                    if (!GameObjects.Any(go => go.DisplayName == updatedProgramList[x].DisplayName))
                        {
                            //if (!addedProgramsList.Contains(updatedProgramList[x]))
                            // If this program is not already in added programs list
                            if (!addedProgramsList.Any(p => p.DisplayName == updatedProgramList[x].DisplayName))
                            {
                                addedProgramsList.Add(updatedProgramList[x]);
                            }
                        }
                    //}
                }
            //}

            // Add only new games
            if (addedProgramsList.Count != 0)
            {
                try
                {
                    // This checks if program is a game, no need to check on full rescan
                    SortGamesFromProgramsAndAddToGrid(addedProgramsList);
                }
                catch (Exception)
                {
                    StopAnimatingLoadingRectangle();
                    CheckGameObjectDBReadyness = false;
                }
            }
        }

        // Crayzee Linq usage
        private static List<Program> GetAddedPrograms()
        {
            return updatedProgramList.Where(updatedProg => !ProgramObjects.Any(prog => updatedProg.DisplayName == prog.DisplayName)).ToList();
        }

        private static List<Program> GetMissingPrograms()
        {
            return ProgramObjects.Where(prog => !updatedProgramList.Any(updatedProg => prog.DisplayName == updatedProg.DisplayName)).ToList();
        }
    }
}
