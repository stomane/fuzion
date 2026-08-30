using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fuzion.IGDB;
using Fuzion.Extensions;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Fuzion.Scanner;
using static Fuzion.MainWindow;
using System.Threading;
using System.Windows;
using System.Linq;
using System.Windows.Threading;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.SettingsManager.GeneralSettings;
using Fuzion.SettingsManager;

namespace Fuzion.Programs
{
    public enum Mark { Exists, New, Default }
    public enum BelongsToLauncher { Steam, BattleNet, Epic, Uplay, GOG, Standalone, UWP, Origin }
    public enum PathType { Path, URI }

    public class Program : IDockable
    {
        public bool IsGame { get; set; }
        public bool IsUserModified { get; set; }
        public bool IsManuallyAdded { get; set; }
        public bool HasDownloadedIcon { get; set; }
        public string DisplayName { get; set; } = "";
        public string DockName { get; set; } = "";
        public string Path { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string UninstallPath { get; set; } = "";
        public string WorkDir { get; set; } = "";
        public string Icon { get; set; } = "";
        public string ChangedIcon { get; set; } = "";
        public string SystemIcon { get; set; } = "";
        public string ExeName { get; set; } = "";
        public string EpicAppName { get; set; } = "";
        public string SteamAppID { get; set; } = "";
        public string UplayAppID { get; set; } = "";
        public string UWPAppID { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string IconURI { get; set; } = "";
        public string IconGUID { get; set; }

        // Database ready
        /// <summary>
        /// Indicates that the program/game is ready to be pushed to db. Usually after all necessary fields such as
        /// icon link and exe name have been processed
        /// </summary>
        private bool _databaseReady;
        public bool DatabaseReady
        {
            get { return _databaseReady; }
            set
            {
                _databaseReady = value;

                if (_databaseReady == true)
                {
                    // Find the game and set its database ready bool
                    Game game = GameObjects.FirstOrDefault(g => g.IconGUID == IconGUID);

                    if (game != null)
                    {
                        game.DatabaseReady = true;
                    }
                }
            }
        }
        private bool _iconFetchComplete;
        public bool IconFetchComplete
        {
            get { return _iconFetchComplete; }
            set
            {
                _iconFetchComplete = value;

                // Check if ExeFetch is also complete and toggle the DatabaseReady bool
                if (_iconFetchComplete == true && ExeFetchComplete == true)
                {
                    //Game g = null;
                    //Application.Current.Dispatcher.Invoke(() => { g = this.ToGame(); });

                    //RecentlyAddedGames.Add(g);
                    //Console.WriteLine("Added URI DBREADY: " + g.IconURI);
                    DatabaseReady = true;
                    Console.WriteLine("DB ready toggle from Icon Fetch");
                }
            }
        }
        private bool _exeFetchComplete;
        public bool ExeFetchComplete
        {
            get { return _exeFetchComplete; }
            set
            {
                _exeFetchComplete = value;

                // Check if IconFetch is also complete and toggle the DatabaseReady bool
                if (_iconFetchComplete == true && ExeFetchComplete == true)
                {
                    //Game g = null;
                    //Application.Current.Dispatcher.Invoke(() => { g = this.ToGame(); });
                    //RecentlyAddedGames.Add(g);
                    //Console.WriteLine("Added URI DBREADY: "+g.IconURI);
                    DatabaseReady = true;
                    Console.WriteLine("DB ready toggle from Exe Fetch");
                }
            }
        }

        // Originals
        public string OriginalPath { get; set; } = "";
        public string OriginalArguments { get; set; } = "";
        public string OriginalIcon { get; set; }

        public int Index { get; set; } = 0;

        public BelongsToLauncher Launcher { get; set; } = BelongsToLauncher.Standalone;
        public PathType PathType { get; set; } = PathType.Path;
        public BelongsToLauncher OriginalLauncher { get; set; } = BelongsToLauncher.Standalone;
        public PathType OriginalPathType { get; set; } = PathType.Path;

        public Program()
        {
            if (string.IsNullOrEmpty(IconGUID))
            {
                IconGUID = Guid.NewGuid().ToString();
                Icon = Fuzion.MainWindow.DefaultAssetPath + @"Icons\" + IconGUID + ".png";
                OriginalIcon = Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\" + IconGUID + ".png";
            }

            if (string.IsNullOrEmpty(DockName))
                DockName = DisplayName;
        }

        private static string GetGUID()
        {
            return Guid.NewGuid().ToString();
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public void FetchIcon()
        {
            if (Properties.Settings.Default.FetchOnlineIcon && Constants.HasGoogleSearchApiKey)
            {
                _ = Icons.IconManager.DownloadIcon(this);
            }
            else
            {
                string jumbo = Icons.IconManager.GetPathToJumboOrNoIcon(this);
                Icons.IconManager.PrepareIconForGrid(jumbo, 1d, this);
            }
        }

        public void FetchExe()
        {
            Path = ExeFinder.GetExePath(WorkDir, this);
        }

        public void UpdateIconLinkData(string link)
        {
            try
            {
                IconURI = link;

                // Update gameobjects
                Game goGame = GameObjects.FirstOrDefault(g => g.IconGUID == IconGUID);

                if(goGame != null)
                    goGame.IconURI = link;

                // Update recentlyadded games list
                Game ragGame = RecentlyAddedGames.FirstOrDefault(g => g.IconGUID == IconGUID);

                if (ragGame != null)
                    ragGame.IconURI = link;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to update program icon link: " + ex.Message);
            }
        }
    }

    public class Game : Image, IDockable
    {
        public bool IsGame { get; set; }
        public bool IsUserModified { get; set; }
        public bool IsManuallyAdded { get; set; }
        public bool HasDownloadedIcon { get; set; }
        public string DisplayName { get; set; }
        public string DockName { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string UninstallPath { get; set; }
        public string WorkDir { get; set; }
        public string Icon { get; set; }
        public string ChangedIcon { get; set; }
        public string SystemIcon { get; set; }
        public string ExeName { get; set; }
        public string EpicAppName { get; set; }
        public string SteamAppID { get; set; }
        public string UplayAppID { get; set; }
        public string UWPAppID { get; set; }
        public string IconURI { get; set; }
        public string IconGUID { get; set; }
        /// <summary>
        /// Indicates that the program/game is ready to be pushed to db. Usually after all necessary fields such as
        /// icon link and exe name have been processed
        /// </summary>

        private bool _databaseReady;
        public bool DatabaseReady
        {
            get { return _databaseReady; }
            set
            {
                _databaseReady = value;

                //if (_databaseReady == true)
                //{
                //    Console.WriteLine(DisplayName + " is db ready!");
                //}
            }
        }


        // Originals
        public string OriginalPath { get; set; }
        public string OriginalArguments { get; set; }
        public string OriginalIcon { get; set; }

        public int Index { get; set; }

        public bool IsRunning { get; set; }

        public ColumnDefinition OwnedColumnDefinition { get; set; }
        public RowDefinition OwnedRowDefinition { get; set; }

        public BelongsToLauncher Launcher { get; set; }
        public PathType PathType { get; set; }
        public BelongsToLauncher OriginalLauncher { get; set; }
        public PathType OriginalPathType { get; set; }

        public Storyboard GrowStoryboard { get; private set; }
        public Storyboard ShrinkStoryboard { get; private set; }
        public Storyboard FadeRemoveStoryboard { get; private set; }
        public Storyboard FadeInStoryboard { get; private set; }
        public Storyboard FadeOutStoryboard { get; private set; }
        public Storyboard FadeOutInStoryboard { get; private set; }
        public Storyboard SlideLeftStoryboard { get; private set; }
        public Storyboard SlideRightStoryboard { get; private set; }

        public Storyboard LaunchStoryboard { get; private set; }
        public DoubleAnimation IconSizeAnimation { get; private set; }


        public override string ToString()
        {
            return DisplayName;
        }

        public void Remove(bool blacklist = false)
        {
            // Blacklist the game only if it was manually removed until a full scan occurs
            if (blacklist)
            {
                Blacklist.Add(DisplayName);
            }

            // scroll one game distance to left/top because we'll hit grid edge
            if (IsHittingRightBottomEdgeOnRemove)
            {
                Console.WriteLine("Hitting Grid Edge, scrolling");
                // Need to override scroll speed to make it happen within GridAnimationLength (200ms)
                // using a timed lerp
                Dock.Scrolling.IsRemovingGame = true;
                Dock.Scrolling.ScrollToIncrement(-ActualGameSize);
            }


            //// scroll one game distance to right/bottom because we'll hit grid edge
            //if (IsHittingLeftTopEdgeOnRemove)
            //{
            //    Console.WriteLine("Hitting Grid Edge, scrolling");
            //    // Need to override scroll speed to make it happen within GridAnimationLength (200ms)
            //    // using a timed lerp
            //    Dock.Scrolling.IsRemovingGame = true;
            //    Dock.Scrolling.ScrollToIncrement(ActualGameSize);
            //}

            BeginStoryboard(ShrinkStoryboard);

            if (IsDockPerfectlyFittingScreen)
            {
                AnimateGridCells(this, GameActionType.Remove);
            }
            else
            {
                AnimateGridCellsZoomed(this, GameActionType.Remove);
            }

        }


        public Game()
        {
            IconSizeAnimation = new DoubleAnimation(ActualWidth, Properties.Settings.Default.StartupIconSize, new TimeSpan(0, 0, 0, 0, gridAnimationLength));
            Storyboard shrinkRemove = (Storyboard)TryFindResource("GameShrinkAnimation");
            Storyboard fadeRemove = (Storyboard)TryFindResource("GameFadeOutInAnimation");
            GrowStoryboard = (Storyboard)TryFindResource("GameGrowAnimation");
            ShrinkStoryboard = shrinkRemove.Clone();
            FadeRemoveStoryboard = fadeRemove.Clone();
            FadeInStoryboard = (Storyboard)TryFindResource("GameFadeInAnimation");
            FadeOutStoryboard = (Storyboard)TryFindResource("GameFadeOutAnimation");
            FadeOutInStoryboard = (Storyboard)TryFindResource("GameFadeOutInAnimation");
            SlideLeftStoryboard = (Storyboard)TryFindResource("GameSlideLeftAnimation");
            SlideRightStoryboard = (Storyboard)TryFindResource("GameSlideRightAnimation");
            LaunchStoryboard = (Storyboard)TryFindResource("GameLaunchAnimation");

            ShrinkStoryboard.Completed += ShrinkRemoveStoryboard_Completed;
            //FadeRemoveStoryboard.Completed += ShrinkRemoveStoryboard_Completed;

            if (string.IsNullOrEmpty(DockName))
                DockName = DisplayName;
        }


        public void AnimateIconSize()
        {
            IconSizeAnimation = new DoubleAnimation(ActualWidth, Properties.Settings.Default.StartupIconSize, new TimeSpan(0, 0, 0, 0, gridAnimationLength));
            BeginAnimation(WidthProperty, IconSizeAnimation);
        }

        private void ShrinkRemoveStoryboard_Completed(object sender, EventArgs e)
        {
            // Return to normal lerping
            Dock.Scrolling.IsRemovingGame = false;

            // I can adjust the scrollviewer in pixels using mediator hOffset
            //AppWindow.Mediator.HorizontalOffset += 20d;

            AppWindow.mainGrid.Children.Remove(this);

            if (IsDockHorizontal)
                AppWindow.mainGrid.ColumnDefinitions.Remove(OwnedColumnDefinition);
            else
                AppWindow.mainGrid.RowDefinitions.Remove(OwnedRowDefinition);

            Program prog = ProgramObjects.FirstOrDefault(p => p.IconGUID == IconGUID);

            GameObjects.Remove(this);

            // remove the program so next rescan can restore initial games list, this needs to be upgraded
            // to something more modular and less request intensive

            if (prog != null && prog.IsGame)
            {
                Console.WriteLine("Removed from program list: " + prog.DisplayName + " with GUID " + prog.IconGUID);
                ProgramObjects.Remove(prog);
            }

            AppWindow.CleanupAfterRemoval();
        }
    }

    public static class ProgramManager
    {
        /// <summary>
        /// Main List of game objects. Grid mirrors this list.
        /// </summary>
        public static List<Game> GameObjects { get; set; } = new List<Game>();

        /// <summary>
        /// Main List of program objects which go into programs.xml
        /// </summary>
        public static List<Program> ProgramObjects { get; set; } = new List<Program>();

        /// <summary>
        /// Used to detect only newly added games which are prepared for the online database.
        /// Once all items in this list have their DatabaseReady bool true, a database push will occur
        /// </summary>
        public static List<string> RecentlyAddedGameNames { get; set; } = new List<string>();
        /// <summary>
        /// A list of games to mirror the recently added game names list, for easier checking of db readyness
        /// </summary>
        public static List<Game> RecentlyAddedGames { get; set; } = new List<Game>();


        //public static List<Program> SortGamesFromPrograms(List<Program> programsList)
        //{
        //    List<Program> result = new List<Program>();

        //    if (programsList != null && programsList.Count > 0)
        //    {
        //        int i = 0;

        //        Parallel.ForEach(programsList, (program, state, index) =>
        //        {
        //            if (GameCheck.IsGame(program))
        //            {
        //                program.FetchIcon();

        //                if(program.Launcher == BelongsToLauncher.Standalone)
        //                {
        //                    program.Path = ExeFinder.GetExePath(program.WorkDir, program);
        //                    program.OriginalPath = program.Path;
        //                }

        //                program.Index = i;
        //                result.Add(program);
        //                i++;
        //            }
        //        });
        //    }

        //    return result;
        //}

        public static void SortGamesFromProgramsAndAddToGrid(List<Program> programList)
        {
            if (programList != null && programList.Count > 0)
            {
                GameCheck.PreloadBatchGameDecisions(programList);

                //Normal loop
                _ = Parallel.ForEach(programList, (program, state, index) =>
                    {
                        ProgramToGrid(program);
                    });

                if (RecentlyAddedGameNames.Count == 0)
                {
                    Console.WriteLine("Parallel foreach exited with no new games, stopping db push listener");
                    StopAnimatingLoadingRectangle();
                    CheckGameObjectDBReadyness = false;
                }
            }
        }

        public static void CheckIsGameAndAddToGrid(Program program)
        {
            if (program != null)
                ProgramToGrid(program);
        }

        private static void ProgramToGrid(Program program)
        {
            AnimateLoadingRectangle(true, "ptg" + program.IconGUID);
            Console.WriteLine("Checking for " + program.DisplayName);
            if (GameCheck.IsGame(program))
            {
                // Add the name to the new games list to check for db readyness before the fetchicon and fetchexe occur
                RecentlyAddedGameNames.Add(program.DisplayName);
                Console.WriteLine("Adding " + program.DisplayName + " to recently added games list");

                // Make sure we run this last as it will update links
                // in GameObjects and RecentlyAddedGames lisits
                program.FetchIcon();

                // Only Origin and Standalone games need to have their exes indexed, Origin needs to be updated to index games
                // straight from the Origin class, GoG is considered standalone for now
                if (program.Launcher == BelongsToLauncher.Standalone || program.Launcher == BelongsToLauncher.Origin)
                {
                    if (indexExecutables)
                    {
                        program.Path = ExeFinder.GetExePath(program.WorkDir, program);
                        program.OriginalPath = program.Path;
                    }
                }

                // Indicate that exe has been indexed for database ready
                program.ExeFetchComplete = true;

                // I have no idea what this does, maybe one day I'll look into it
                program.HasDownloadedIcon = false;

                // Add the game after indexing exe but before getting an icon
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    Game game = program.ToGame();
                    RecentlyAddedGames.Add(game);
                    GameObjects.Add(game);
                    AppWindow.AddGameToGrid(game);
                }));


            }

            AnimateLoadingRectangle(false, "ptg" + program.IconGUID);
        }
    }
}
