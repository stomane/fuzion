using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Animation;
using Fuzion.Properties;
using Fuzion.Programs;
using Fuzion.WindowsManager;

using static Fuzion.Programs.Launch;
using static Fuzion.Programs.Serialization;
using static Fuzion.Native.NativeMethods;
using static Fuzion.Programs.ProgramManager;
using static Fuzion.Update.UpdateHandler;
using static Fuzion.SettingsManager.GeneralSettings;
using static Fuzion.Network.Connectivity;
using Fuzion.Native.IdleHook;
using System.Threading.Tasks;
using Fuzion.Extensions;
using static Fuzion.Scanner.Scan;
using static Fuzion.Cleanup.RemoveObsolete;
using static Fuzion.Native.IdleHook.HookManager;
using SteamStoreQuery;
using System.Security.Policy;
using System.Net;
using Fuzion.Gamepad;
using Fuzion.SettingsManager;
using Fuzion.Icons;
using SharpDX.Win32;
using System.Windows.Threading;
using Fuzion.Analytics;
using Fuzion.Update;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using Fuzion.Dock;
using System.Windows.Interop;

namespace Fuzion
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    // Check out Steamkit2 nuget package
    // Record comparison video of game detection between fuzion, playnite, gog 2, geforce xp

    // Probably should purchase certificate from KSoftware.net
    // A certificate from CA will be necessary for ClickOnce too, in the meantime use Test Certificates. Test Certs do not build trust
    // Useful links:
    // https://docs.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions

    // Distribution details MSIX
    // Generated Assets folder location:
    // User Settings location:
    // What happens on update:
    // 1. All versions higher: rescan occured
    // 2. Change only manifest version before export:

    // Update flow:
    // 0. Switch to Release
    // 1. Change assembly version Project>Properties>Application>Assembly Information, Build
    // 2. Open latest .nupkg from base dir and apply any changes to files and meta, Save As with the new version (dont add .pdb files!!!)
    // 3. Tools>Nuget Package Manager>Package Manager Console: Squirrel --releasify Fuzion.0.5.0.nupkg
    // 4. Upload to server from Releases folder in base dir

    // Caveats
    // UpdateSettings() will overwrite existing game data if called before the current data is loaded

    // Issues
    // Rearranging icons doesn't animate after rescan

    // To-do ------------------------------------------------------
    // Finish analytics to use registry key instead of user settings
    // Finish Exception Reporting class in SQL
    // Some issues with mouse hit testing on top of first icon when changing DPI
    // Look into replacing Forms NotifyIcon with something like this: https://marketplace.visualstudio.com/items?itemName=PhilippSumi.WPFNotifyIcon&ssr=false#overview
    // Finish optimization at Icons.IconManager.ScanImageFromURL() to speed up icon finding
    // Add interactive features page on the website which will show you what fuzion can and will do in the future
    // ^^ Add a simpler features overview page where each feature is listed

    // Make animated icons for Fuzion and try it out (like cinemagraphs but icons)
    // add hide taskbar icon setting - needs to be added with stick to desktop
    // add a setting to show fuzion from a hot corner or when mouse is at top of display or bottom
    // Return UWP system icon indexing, it's buggy so it's disabled temporarily
    // IconManager - icon scanning needs to happen directly from the web stream
    // Folder cleanup needs improvements
    // Launcher classes need to get an interface(ILauncher)
    // Move Game and Program to separate classes
    // Check the Process Tree Monitor that Playnite uses to detect playtime and use that to better detect when a game has exited and re-add origin
    // Move all native methods to the same class and call them from there

    // Minor ------------------------------------------------------
    // Battle.net game detection needs to be improved
    // Revert icon should revert to system icon if it exists and to normal icon if it doesn't - ideally
    // GoG games are indexed as Standalone, could read the data from GoG and provide option to start GoG launcher when launching game

    // Testing ----------------------------------------------------
    // Scrolling in different orientations, edge bounce, fade area
    // Add remove game animations in different orientations
    // Multi monitor tests

    #region Converters
    public class ScrollViewerBackgroundColor : IValueConverter
    {
        //Color.FromArgb(255, 100, 100, 100);)
        //Color.FromArgb(255, 255, 255, 255);)
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Settings.Default.DarkGameOutline
                ? new SolidColorBrush(Color.FromArgb(25, 0, 0, 0))
                : new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PercentageOfElement : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * (double)parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value / (double)parameter;
        }
    }

    public class ContextMenuEditHeader : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            //return "_Edit";
            if (MainWindow.RightClickedGame != null)
                return "_Edit " + MainWindow.RightClickedGame.DisplayName;
            else
                return "_Edit";
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Scrollviewer scrollablewidth sample calc = scrollableWidth * lowerscrolllimit(0.1) + MainWindow.GradientOffsetPixels(StartupIconSize/2)
    /// </summary>
    public class ScrollViewerOffsets : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((double)value == 0d)
            {
                return (double)value;
            }
            else
            {
                return (double)value * (double)parameter + MainWindow.GradientOffsetPixels;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    public partial class MainWindow : Window
    {
        public static Game RightClickedGame { get; private set; }
        public static double ActualGameSize => IsDockHorizontal
                    ? Settings.Default.StartupIconSize + (DefaultGameMargins.Left * 2)
                    : Settings.Default.StartupIconSize + (DefaultGameMargins.Top * 2);
        public static bool IsDockHorizontal => Settings.Default.DockLocation <= 1;

        #region Path Constants
        public static string DefaultAssetPath { get; } = GetDefaultAssetPath();
        public static string DefaultSettingsPath { get; } = GetDefaultSettingsPath();

        private static double MainScreenRelativeWidth { get; set; }
        private static double MainScreenRelativeHeight { get; set; }
        #endregion

        public static bool LaunchedFromStartup { get; set; }
        public static bool DockScrollingDisallowed { get; private set; }

        // Constant
        public const int gridAnimationLength = 200; // is 200ms
        private const bool reindex = false;
        public const bool indexImages = true;
        public const bool indexExecutables = true;

        public const bool allowDuplicates = true;

        private static Storyboard loadingRectStoryboard;
        private static Storyboard loadingRectShrinkStoryboard;

        public static MainWindow AppWindow { get; set; }

        public enum CenterDock { Top, Bottom }

        private Border GameHighlightBorder;

        public static Game HighlightedGame { get; set; }

        private static Image BigAddButton { get; set; }

        /// <summary>
        /// Value is updated when dock location is changed
        /// </summary>
        public static Thickness DefaultGameMargins { get; private set; }
        /// <summary>
        /// Value is updated when dock location is changed
        /// </summary>
        public static Thickness DefaultChatBarThickness { get; private set; }

        public static PercentageOfElement PercentageOfElementConverter { get; } = new PercentageOfElement();
        public static ScrollViewerOffsets ScrollViewerOffsetsConverter { get; } = new ScrollViewerOffsets();
        public static ScrollViewerBackgroundColor ScrollViewerBackgroundColorConverter { get; } = new ScrollViewerBackgroundColor();
        public static double ScrollVisibleIconCount { get; private set; }
        public enum InputSource { Mouse, Keyboard, Gamepad }
        public static InputSource LastInputSource { get; set; }


        public static bool MainWindowActive { get; private set; }
        public static IntPtr Handle { get; private set; }
        public static bool IsZoomActive { get; private set; }

        private static string GetDefaultAssetPath()
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Fuzion\"));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Fuzion\\");
        }

        private static string GetDefaultSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Tzar\\");
        }

        private static void SetMainScreenRelativeWidthHeight()
        {
            double sWidth = Position.Monitors.ActiveScreen.Bounds.Width;//SystemParameters.PrimaryScreenWidth;
            double mainScreenRelativeWidth = sWidth;

            double sHeight = Position.Monitors.ActiveScreen.Bounds.Height; //SystemParameters.PrimaryScreenHeight;
            double mainScreenRelativeHeight = sHeight;

            PresentationSource source = PresentationSource.FromVisual(AppWindow);
            if (source != null)
            {
                //dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                //dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                // Get 100% scale resolution
                mainScreenRelativeWidth = sWidth * dpiScaleX;
                mainScreenRelativeHeight = sHeight * dpiScaleY;
            }

            Console.WriteLine("MainScreenRelativeWidth set to: " + mainScreenRelativeWidth);
            Console.WriteLine("MainScreenRelativeHeight set to: " + mainScreenRelativeHeight);
            MainScreenRelativeWidth = mainScreenRelativeWidth;
            MainScreenRelativeHeight = mainScreenRelativeHeight;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Set Default Asset Path - moved to static initializers

            // Set public static MW access
            AppWindow = this;

            // Initialize Loading Rect Animations
            loadingRectStoryboard = AppWindow.TryFindResource("MorphAnimation") as Storyboard;
            loadingRectShrinkStoryboard = AppWindow.TryFindResource("GameShrinkAnimation") as Storyboard;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) //Runs once
        {
            Startup();
            InitEvents();
            InitSettings();
            CheckForSettings();
            EnsureVisibleAfterStartup();
            AddSearchKeyPressEvents();
            _ = Task.Run(() => SquirrelUpdate.Update());
            _ = Task.Run(() => SettingsWindow.GetDynamicImages());
            
            // Fix icon margins to remove gaps in auto-sized background
            UpdateIconMargins();
            //StartTestDispatcher();
        }
        private void Startup()
        {
            SetMainScreenRelativeWidthHeight();
            CreateDirectories();
            if (ShouldUseLegacyDesktopDocking())
            {
                HideFromTaskSwitcher();
            }
            SetupMinimizePreventionHook();
            //Dock.Scrolling.EnableSmoothScrolling();
            ScrollTimer.Start();
            CreateTaskbarIcon();
            ChangeAddRemoveProgramsIcon();
            InitializeUniversalLaunchOnStartup();
            AnalyticsHelper.Initialize();
            SetDockLocationDefaultValues();
            CreateGameTooltip();
            //TrayIcon.FocusFuzionOnClick();
        }


        private IntPtr foregroundHook;
        private static Fuzion.Native.NativeMethods.WinEventDelegate foregroundDelegate; // Keep reference to prevent GC

        /// <summary>
        /// Sets up a window event hook to prevent the dock from being hidden when Windows+D is pressed
        /// </summary>
        private void SetupMinimizePreventionHook()
        {
            // Use EVENT_SYSTEM_FOREGROUND hook to detect when WorkerW becomes foreground (Show Desktop)
            foregroundDelegate = new Fuzion.Native.NativeMethods.WinEventDelegate(OnForegroundWindowChanged);
            foregroundHook = Fuzion.Native.NativeMethods.SetWinEventHook(
                3, 3, // EVENT_SYSTEM_FOREGROUND
                IntPtr.Zero, foregroundDelegate,
                0, 0, 0); // WINEVENT_OUTOFCONTEXT
        }

        /// <summary>
        /// Called when foreground window changes - detects Show Desktop and keeps window visible
        /// </summary>
        private void OnForegroundWindowChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            var className = new StringBuilder(256);
            Fuzion.Native.NativeMethods.GetClassName(hwnd, className, className.Capacity);
            
            System.IO.File.AppendAllText(@"C:\temp\fuzion_debug.txt", DateTime.Now.ToString("HH:mm:ss") + " Foreground changed to: " + className.ToString() + " (hwnd: " + hwnd + ")`r`n");
            
            if (className.ToString() == "Progman" || className.ToString() == "WorkerW")
            {
                System.IO.File.AppendAllText(@"C:\temp\fuzion_debug.txt", DateTime.Now.ToString("HH:mm:ss") + " ***DETECTED WorkerW - Setting Topmost***`r`n");
                // Show Desktop was triggered, keep our window visible by setting Topmost
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Topmost = true;
                    System.IO.File.AppendAllText(@"C:\temp\fuzion_debug.txt", DateTime.Now.ToString("HH:mm:ss") + " Topmost set to true`r`n");
                }));
            }
            else if (Topmost)
            {
                System.IO.File.AppendAllText(@"C:\temp\fuzion_debug.txt", DateTime.Now.ToString("HH:mm:ss") + " Non-WorkerW detected while Topmost, resetting`r`n");
                // Another window is foreground, allow normal layering
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Topmost = false;
                }));
            }
        }

        /// <summary>
        /// Window procedure hook to intercept minimize messages
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;

            // Intercept and block minimize command
            if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_MINIMIZE)
            {
                // Prevent minimize by marking the message as handled
                handled = true;
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Prevents the window from being minimized (Windows 11 compatible)
        /// </summary>
        protected override void OnStateChanged(EventArgs e)
        {
            // Prevent minimize completely - don't call base if minimizing
            if (WindowState == WindowState.Minimized)
            {
                // Force back to normal without processing the minimize
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    WindowState = WindowState.Normal;
                    Show();
                    Topmost = true;
                    Topmost = false; // Flash to ensure visibility
                }), DispatcherPriority.Send);
                return; // Don't call base.OnStateChanged for minimize
            }
            base.OnStateChanged(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Handle = new WindowInteropHelper(this).Handle;
        }

        //private static void ScrollTimerTick(object state)
        //{
        //    // Handle database push from here
        //    if (checkGameObjectDBReadyness)
        //    {
        //        CheckForDatabasePush();
        //    }

        //    // Console.WriteLine("ScrollTimerTicked");// will spam the console
        //    // has not reached target within offset, keep lerping
        //    //Application.Current.Dispatcher.Invoke(new Action(() =>
        //    //{
        //    //    Console.WriteLine("Left: " + AppWindow.Left);
        //    //}));


        //    if (scrollViewerLerper < SmoothScrollTarget - scrollTargetOffset
        //            || scrollViewerLerper > SmoothScrollTarget + scrollTargetOffset)
        //    {
        //        scrollViewerLerper = MathExtensions.Lerp(scrollViewerLerper, SmoothScrollTarget, FinalScrollLerpSpeed); // was 0.007

        //        if (isRearrangingGrid)
        //            Application.Current.Dispatcher.Invoke(new Action(() => { UpdateGridDragPoints(); }));

        //        Application.Current.Dispatcher.Invoke(new Action(() =>
        //        {
        //            if (GameTooltip.IsOpen)
        //            {
        //                var mi = typeof(Popup).GetMethod("UpdatePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        //                mi.Invoke(GameTooltip, null);
        //            }
        //        }));

        //    }
        //    else // has reached target
        //    {
        //        //// Update drag points for rearranging grid
        //        //if (canIssueGridPointUpdate && isRearrangingGrid)
        //        //{
        //        //    Console.WriteLine("Updating Grid Drag from LERPER");
        //        //    Application.Current.Dispatcher.Invoke(new Action(() => { UpdateGridDragPoints(); }));
        //        //    canIssueGridPointUpdate = false;
        //        //}
        //    }

        //    ScrollEdgeBounce();

        //    Application.Current.Dispatcher.Invoke(new Action(() =>
        //    {
        //        if (Settings.Default.DockLocation <= 1)
        //        {
        //            AppWindow.Mediator.ScrollableWidthMultiplier = scrollViewerLerper;
        //        }
        //        else
        //        {
        //            AppWindow.Mediator.ScrollableHeightMultiplier = scrollViewerLerper;
        //        }

        //        //// Adjust scrollviewer position when changing icon size - needs improvement it'll lock the scroll
        //        //// Why is the scrollviewer moving from top anyway? is it alignment issue? could be transform origin center - check
        //        //if (OpenWindowsManager.IsWindowOpen("Settings"))
        //        //{
        //        //    // Update scrollviewer lerp position
        //        //    if (AppWindow.GridScrollViewer.ScrollableHeight != 0)
        //        //    {
        //        //        //double scrollTarget = AppWindow.GridScrollViewer.VerticalOffset / AppWindow.GridScrollViewer.ScrollableHeight;
        //        //        scrollViewerOffsetLerper = MathExtensions.Lerp(scrollViewerOffsetLerper, scrollViewerLerper, 0.1d);
        //        //        AppWindow.GridScrollViewer.ScrollToVerticalOffset(AppWindow.GridScrollViewer.ScrollableHeight * scrollViewerOffsetLerper);
        //        //    }

        //        //}
        //    }));

        //    try
        //    {
        //        ScrollTimer.Change(1, System.Threading.Timeout.Infinite);
        //    }
        //    catch (ObjectDisposedException)
        //    {
        //        // sometimes happens when exiting fuzion
        //    }
        //}

        public static bool CheckGameObjectDBReadyness { get; set; }

        public static void CheckForDatabasePush()
        {
            //Console.WriteLine("Checking db push");
            // Wait for the list to pick up the count
            // Otherwise this will be stopped from SortGamesFromProgramsAndAddToGrid() if count is 0
            if (RecentlyAddedGameNames.Count > 0)
            {
                StopAnimatingLoadingRectangle();

                // If all newly added gameobjects are database ready, then push to db
                if (RecentlyAddedGames.All(go => go.DatabaseReady))
                {
                    CheckGameObjectDBReadyness = false;
                    Console.WriteLine("Pushing to Fuzion online database");
                    LocalDatabase.UpdateDatabaseProgramsAsync(true);
                }
            }
        }

        private static void SetDockLocationDefaultValues()
        {
            if (Settings.Default.DockLocation <= 1) // top, bottom
            {
                DefaultGameMargins = new Thickness(Settings.Default.IconSpacing, 5, Settings.Default.IconSpacing, 5);

                if (Settings.Default.DockLocation == 0)
                    DefaultChatBarThickness = new Thickness(0, 0, 0, 8);

                if (Settings.Default.DockLocation == 1)
                    DefaultChatBarThickness = new Thickness(0, 8, 0, 0);
            }
            else // left,right
            {
                DefaultGameMargins = new Thickness(5, Settings.Default.IconSpacing, 5, Settings.Default.IconSpacing);

                if (Settings.Default.DockLocation == 2)
                    DefaultChatBarThickness = new Thickness(0, 0, 8, 0);

                if (Settings.Default.DockLocation == 3)
                    DefaultChatBarThickness = new Thickness(8, 0, 0, 0);
            }
        }

        private static void ClearLocalDatabaseOnce()
        {
            if (Settings.Default.ClearLocalDatabase)
            {
                string programsPath = Path.Combine(LocalDatabase.Path, LocalDatabase.ProgramsFileName);
                string gamesPath = Path.Combine(LocalDatabase.Path, LocalDatabase.GamesFileName);

                if (File.Exists(programsPath))
                {
                    File.Delete(programsPath);
                }

                if (File.Exists(gamesPath))
                {
                    File.Delete(gamesPath);
                }

                Settings.Default.ClearLocalDatabase = false;
                Settings.Default.Save();
            }
        }

        private static async void InitializeUniversalLaunchOnStartup()
        {
            if (UniversalPlatform.Startup.IsUniversalPlatform == true)
            {
                Settings.Default.LaunchOnStartup = await UniversalPlatform.Startup.GetCurrentStartupState().ConfigureAwait(false);
                Settings.Default.Save();
            }
            else
            {
                ManageStartupRegistryKey(Settings.Default.LaunchOnStartup);
            }
        }

        public static void ManageStartupRegistryKey(bool enabled)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (enabled)
            {
                registryKey.SetValue("Fuzion", $"{System.Reflection.Assembly.GetExecutingAssembly().Location} -startup");
            }
            else
            {
                registryKey.DeleteValue("Fuzion");
            }
        }

        private void CheckForSettings()
        {
            if (reindex)
            {
#pragma warning disable CS0162 // Unreachable code detected
                try
#pragma warning restore CS0162 // Unreachable code detected
                {
                    File.Delete(DefaultAssetPath + @"programs\programs.xml");
                    File.Delete(DefaultAssetPath + @"programs\games.xml");
                }
                catch (Exception)
                {

                }
            }

            try
            {
                //Check if settings exist
                if (File.Exists(Fuzion.MainWindow.DefaultAssetPath + @"programs\programs.xml")
                && File.Exists(Fuzion.MainWindow.DefaultAssetPath + @"programs\games.xml"))
                {
                    Console.WriteLine("Files exist, loading...");
                    Load();
                }
                else
                {
                    Console.WriteLine("No saved dock data found. Creating grid for initial scan.");
                    CreateGrid();

                    if (Constants.HasIgdbProxyUrl && HasNetworkConnection("https://igdb.com"))
                    {
                        Console.WriteLine("Starting initial scan with online metadata.");
                    }
                    else
                    {
                        Console.WriteLine("Starting initial scan in offline mode.");
                    }

                    _ = Task.Run(() => DeepScan());
                }

            }
            // Handle exception on startup
            catch (Exception)
            {
                // Just load the initial grid if initial scan is unable to execute
                // WAS ENABLED
                Dispatcher.BeginInvoke(new Action(() =>
                OpenWindow.Notification("Unexpected exception. Try restarting Fuzion or resetting it from Settings > Reset", "Startup Error")

                ));

                CreateGrid();
            }

            TestArea();
        }

        // Fall back to a normal top-level window when desktop docking leaves the window hidden.
        private void EnsureVisibleAfterStartup()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Handle == IntPtr.Zero || Native.NativeMethods.IsWindowVisible(Handle))
                {
                    return;
                }

                Console.WriteLine("Main dock window is hidden after startup. Falling back to a visible top-level window.");

                if (Settings.Default.StickToDesktop)
                {
                    SetOnDesktop(this, false);
                }

                WindowState = WindowState.Normal;
                Visibility = Visibility.Visible;
                Show();
                Topmost = true;
                Topmost = false;
                CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
                Activate();
            }), DispatcherPriority.ContextIdle);
        }



        private Native.GlobalHotkeyManager ghm;
        //private static Native.TaskbarSpecific.Taskbar taskBar;
        private void TestArea()
        {
            //taskBar = new Native.TaskbarSpecific.Taskbar();
            //Console.WriteLine("Taskbar w:{0}, h:{1} - hide:{2}", taskBar.Size.Width, taskBar.Size.Height, taskBar.AutoHide);
            //_ = Blacklist.Get();

            //ActivateStickToDesktop(); //will display active window title and class on proc

            // Try using pointers from UWP to get mouse/touch/stylus info instead of raw mouse/keyboard
            // https://docs.microsoft.com/en-us/windows/uwp/design/input/handle-pointer-input
            //UniversalPlatform.EventRegistration.RegisterPointerEvents(GridScrollViewer);

            // Used to register Ctrl + ~ Hotkey, doesn't work with UWP
            //ghm = new Native.GlobalHotkeyManager();


            //Icons.IconManager.ScanImageFromURL("https://images-wixmp-ed30a86b8c4ca887773594c2.wixmp.com/f/5a344cbd-f1b5-47c1-b639-2e2a5fbd2efe/d921wqp-925f360c-c343-46be-9079-8c223b6daf66.png?token=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1cm46YXBwOiIsImlzcyI6InVybjphcHA6Iiwib2JqIjpbW3sicGF0aCI6IlwvZlwvNWEzNDRjYmQtZjFiNS00N2MxLWI2MzktMmUyYTVmYmQyZWZlXC9kOTIxd3FwLTkyNWYzNjBjLWMzNDMtNDZiZS05MDc5LThjMjIzYjZkYWY2Ni5wbmcifV1dLCJhdWQiOlsidXJuOnNlcnZpY2U6ZmlsZS5kb3dubG9hZCJdfQ.0QsIxpTWrdE-UvENShqH6HFilzYS2aHIvWSjW0gSZx4");
            //Icons.IconManager.ScanImageFromURL("https://i.imgur.com/IZmzrN7.png");



            //UWPBindings.StartTimer(); // probably works for 360 controller but not for xbox one
            //GameDeals.DealChecker.CheckCurrentGameDeals();

            //var lswa = new Native.LimitScreenWorkingArea();
            //lswa.Region = new System.Drawing.Region(new System.Drawing.RectangleF(0, 0, 0, 0));
            //lswa.RegisterBar(true);

        }

        private static System.Windows.Threading.DispatcherTimer dealCheckerTimer = new System.Windows.Threading.DispatcherTimer();

        private static void StartDealCheckTimer(Grid dealGrid)
        {
            //if (dealCheckerTimer.IsEnabled == false)
            //{
            //    dealGrid.Visibility = Visibility.Visible;
            //    //GameDeals.DealChecker.LoadDeals();
            //    dealCheckerTimer.Interval = TimeSpan.FromMinutes(5d);
            //    dealCheckerTimer.Tick += DealCheckerTimer_Tick;
            //    dealCheckerTimer.Start();
            //}
        }

        private static void DealCheckerTimer_Tick(object sender, EventArgs e)
        {
            //GameDeals.DealChecker.LoadDeals();
        }

        private static void Load()
        {
            LoadFromSettings();
            RefreshGrid();
            UpdateSettings();
            CleanEverythingParallel();
        }

        private void StartTestDispatcher()
        {
            System.Windows.Threading.DispatcherTimer testDisp = new System.Windows.Threading.DispatcherTimer();
            testDisp.Interval = TimeSpan.FromSeconds(5d);
            testDisp.Tick += TestDisp_Tick;
            testDisp.Start();
        }

        private void TestDisp_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("Vertical Offset "+GridScrollViewer.VerticalOffset);
        }

        private void InitEvents()
        {
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
            Native.ThreadedHook.EnableMandatoryHooks();
            //DeltaTime.Initialize();
        }


        private void SystemParameters_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //Console.WriteLine("Property changed "+ e.PropertyName);

            if (e.PropertyName == "WorkArea")
            {
                CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        private void InitSettings()
        {
            UpgradeSettings();

            #region Debugging Options
            // Open debug window
            if (Settings.Default.OpenDebugWindow)
                Debug.DebugConsole.OpenDebugWindow();

            //Debug.CatchUnhandledExceptions.EnableMessageBoxOnUnhandledException(Settings.Default.ShowUnhandled);
            // Show Unhandled exceptions is now always on
            Debug.CatchUnhandledExceptions.EnableMessageBoxOnUnhandledException(true);

            #endregion

            // Stick to desktop - now always on
            // Settings.Default.StickToDesktop = true;

            // Icon relevance now always 5 until icon database is built
            Settings.Default.IconsPerGame = 5;

            if (Settings.Default.StickToDesktop)
            {
                if (ShouldUseLegacyDesktopDocking())
                {
                    //ActivateStickToDesktop(); //old method
                    SetOnDesktop(this, true);
                }
                else
                {
                    Console.WriteLine("Skipping legacy desktop docking on this Windows build. Showing a normal dock window instead.");
                    ShowInTaskbar = true;
                }
            }

            // Shadow Launch
            LauncherSpecific.ShadowLaunch.UpdateState();

            // Outline color
            if (Settings.Default.DarkGameOutline)
            {
                Application.Current.Resources["Game.ShadowColor"] = Color.FromArgb(255, 100, 100, 100);
            }
            else
            {
                Application.Current.Resources["Game.ShadowColor"] = Color.FromArgb(255, 255, 255, 255);
            }

            // Gamepad
            if (Settings.Default.EnableGamepad)
                Task.Run(() => Bindings.InitializeXInput());

            // Lock the grid
            Settings.Default.IsGridLocked = true;

            // Init Auto Scan for Games
            ToggleAutoScanForGames();

            // Reset default startupiconsize
            Settings.Default.StartupIconSize = Settings.Default.OriginalIconSize;
        }

        private static void UpgradeSettings()
        {
            // This runs once when the app updates
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();

                if (UniversalPlatform.Startup.IsUniversalPlatform)
                {
                    // Load UWP settings into default settings
                    UniversalPlatform.OnUpdate.LoadUWPSettings();
                }
                else
                {
                    Console.WriteLine("Settings Upgrade Running");
                    //Upgrade for Squirrel
                    SquirrelUpdate.RestoreSettings();

                    Settings.Default.Upgrade();
                    Settings.Default.Save();
                    Settings.Default.Reload();
                }
            }
        }

        private static void CreateTaskbarIcon()
        {
            _ = new Icons.TrayIcon();
        }

        private static bool ShouldUseLegacyDesktopDocking()
        {
            return Settings.Default.StickToDesktop && Native.NativeMethods.SupportsLegacyDesktopDocking();
        }

        #region Chat Apps
        private void SteamFriendsChat_MouseDown(object sender, MouseButtonEventArgs e) => OpenChatApp(ChatApp.SteamFriends);

        private void DiscordChat_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenChatApp(ChatApp.Discord);
        }
        private enum ChatApp { Discord, SteamFriends }
        private static void OpenChatApp(ChatApp app = ChatApp.Discord) //add minimize maximize discord and steam if already running
                                                                       // https://social.msdn.microsoft.com/Forums/vstudio/en-US/4fe60f2b-bf5c-45fe-bf9b-332a331d1722/make-another-application-activefocus?forum=wpf
        {
            try
            {
                //string discordArguments = "--processStart Discord.exe";

                if (app == ChatApp.Discord)
                {
                    Process startDiscord = new Process();
                    startDiscord.StartInfo.FileName = LauncherSpecific.Discord.Path;
                    startDiscord.StartInfo.Arguments = LauncherSpecific.Discord.Arguments;
                    startDiscord.Start();
                    startDiscord.Dispose();
                }
                else
                {
                    Process.Start(@"steam://open/friends");
                }
            }
            catch (Exception)
            {

            }

        }
        #endregion


        public static bool LoaderAnimating { get; private set; }
        public static List<string> loaderTaskIDs = new List<string>(); //change back to private, public only so i can add to watch

        public static void AnimateLoadingRectangle(bool animate, string elementName)
        {
            if (animate)
                Console.WriteLine("Animating Rectangle " + elementName);

            if (animate)
            {
                loaderTaskIDs.Add(elementName);
            }
            else
            {
                loaderTaskIDs.Remove(elementName);
            }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (animate)
                {
                    if (!LoaderAnimating)
                    {
                        LoaderAnimating = true;
                        AppWindow.LoadingRectangle.Visibility = Visibility.Visible;
                        loadingRectStoryboard.Begin(AppWindow.LoadingRectangle, true);
                    }
                }

                // stop animating because all tasks are gone
                if (loaderTaskIDs.Count == 0)
                {
                    loadingRectStoryboard.Stop(AppWindow.LoadingRectangle);
                    AppWindow.LoadingRectangle.BeginStoryboard(loadingRectShrinkStoryboard);
                    LoaderAnimating = false;

                    UpdateSettings();
                }
            }));

            Console.WriteLine("Loader task last animate bool: " + animate);
            Console.WriteLine("Loader Task ID count: " + loaderTaskIDs.Count);
        }

        public static void StopAnimatingLoadingRectangle()
        {
            loaderTaskIDs.Clear();

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (LoaderAnimating)
                {
                    loadingRectStoryboard.Stop(AppWindow.LoadingRectangle);
                    AppWindow.LoadingRectangle.BeginStoryboard(loadingRectShrinkStoryboard);
                    LoaderAnimating = false;

                    UpdateSettings();
                }
            }));
        }

        // Change icon in add remove programs
        private static void ChangeAddRemoveProgramsIcon()
        {
            const string fuzionKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Fuzion";

            try
            {
                RegistryKey uninstallKey = Registry.CurrentUser.OpenSubKey(fuzionKeyPath, true);

                if (uninstallKey != null
                    && uninstallKey.GetValue("DisplayIcon") != null
                    && uninstallKey.GetValue("DisplayIcon").ToString() == AppDomain.CurrentDomain.BaseDirectory + @"Assets\logo.ico" + ",0")
                {
                    uninstallKey.SetValue("DisplayIcon", AppDomain.CurrentDomain.BaseDirectory + @"Assets\logo.ico" + ",0");
                }
            }
            catch (Exception)
            {

            }
        }

        private static void CreateDirectories()
        {
            DirectoryInfo newDir = Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"Icons\");
            newDir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

            newDir = Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"icons\"); //this will replace Icons
            newDir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

            newDir = Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"temp\"); //this will replace Icons
            newDir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;

            newDir = Directory.CreateDirectory(Fuzion.MainWindow.DefaultAssetPath + @"Icons\changed\");
            newDir.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Position.Monitors.UpdateScreenIndexes();
            CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
            Console.WriteLine("Display Settings Changed");
            Console.WriteLine("Monitor count: " + System.Windows.Forms.Screen.AllScreens.Length);
            //UpdateGameIconSizes();
        }

        #region Context Menu Buttons

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            AddGameManually();
            Console.WriteLine("Adding game" + sender);
        }

        private void RemoveGameButton_Click(object sender, RoutedEventArgs e)
        {
            var result = OpenWindow.Notification("If this is not a game, check the Blacklist checkbox so it doesn't come back on Rescan.\n\nYou can clear blacklisted apps from Settings",
                $"Remove {RightClickedGame.DockName}?",
                OpenWindow.NotificationWindowType.RemoveGame);

            if (result == OpenWindow.NotificationResult.Yes)
            {
                RightClickedGame.Remove();
            }

            if (result == OpenWindow.NotificationResult.YesBlacklist)
            {
                RightClickedGame.Remove(true);
            }
        }

        private void EditGameButton_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow.Instance.EditGame();
        }

        private void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            RescanAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow.Instance.Settings();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            GracefulShutdown();
        }

        private void ExitDockButton_Click(object sender, RoutedEventArgs e)
        {
            var result = OpenWindow.Notification("Are you sure you want to exit?", $"Close Fuzion?", OpenWindow.NotificationWindowType.YesNo);

            if (result == OpenWindow.NotificationResult.Yes)
            {
                GracefulShutdown();
            }
        }

        #endregion

        public void AddGameManually(bool addFromDrop = false, string pathToDroppedFile = "")
        {
            if (!addFromDrop)
            {
                OpenFileDialog fileDialog = new OpenFileDialog
                {
                    Filter = "Exe, URL, Shortcut (*.exe;*.url;*.lnk)|*.exe;*.url;*.lnk",
                    FilterIndex = 1,
                    Multiselect = false,
                    Title = "Add Game"
                };

                bool? dialog = fileDialog.ShowDialog();

                if (dialog == true)
                {
                    ParseManuallyAddedGame(fileDialog.FileName);
                }
            }

            // File Dropped onto the dock
            if (addFromDrop)
            {
                ParseManuallyAddedGame(pathToDroppedFile);
            }

        }

        private void ParseManuallyAddedGame(string parsedPath)
        {
            string path;
            string ext = Path.GetExtension(parsedPath);
            string displayName = Scanner.ExeFinder.RemoveUnwantedStrings(Path.GetFileNameWithoutExtension(parsedPath));

            switch (ext)
            {
                case ".exe":
                    Console.WriteLine("EXE Chosen" + displayName);
                    GameToGridFromManualSelection(displayName, parsedPath, "", PathType.Path);
                    GameObjects[GameObjects.Count - 1].Focus();
                    break;

                case ".url": //need to get arguments for URL too
                    path = ShortcutUtilities.GetInternetShortcut(parsedPath);
                    Console.WriteLine("URL Chosen: " + path);
                    GameToGridFromManualSelection(displayName, path, "", PathType.URI);
                    GameObjects[GameObjects.Count - 1].Focus();
                    break;

                case ".lnk":
                    List<string> parsed = ShortcutUtilities.GetTargetPath(parsedPath);
                    path = parsed[0] + " " + parsed[1]; //path + arguments
                    Console.WriteLine("LNK Chosen: " + path);
                    GameToGridFromManualSelection(displayName, parsed[0], parsed[1], PathType.Path);
                    GameObjects[GameObjects.Count - 1].Focus();
                    //Console.WriteLine("Link parse 0: "+parsed[0]);
                    break;

                default:
                    // Unsupported extension - don't show notification box - annoying
                    //OpenWindow.Notification(Properties.Resources.UnsupportedFormatMessage);
                    break;
            }
        }

        private void GameToGridFromManualSelection(string displayName = "", string path = @"C:\", string arguments = "", PathType pathType = PathType.Path)
        {
            // if path returns null, I can check the name for a match in UWP apps and get the info from there. That's how to add a UWP game.
            // if that fails then show message.

            try
            {
                Program prog = new Program
                {
                    IsManuallyAdded = true,
                    DisplayName = displayName,
                    DockName = displayName,
                    Path = path,
                    OriginalPath = path,
                    Arguments = arguments,
                    OriginalArguments = arguments,
                    PathType = pathType,
                    OriginalPathType = pathType,
                    Index = 0,
                    SystemIcon = path,
                    WorkDir = Path.GetDirectoryName(path)
                };

                // Remove this from blacklist as the user is manually re-adding it
                Blacklist.Remove(prog.DisplayName);

                prog.FetchIcon(); // Get icon for manually added programs

                Game game = prog.ToGame();
                GameObjects.Add(game);
                AddGameToGrid(game);
                //RefreshGrid(); //called in addgametogrid
                //UpdateSettings(); //called in addgametogrid
            }
            catch (Exception)
            {
                OpenWindow.Notification("Something went wrong...", UiText.UnsupportedFormatMessage);
            }
        }

        #region Grid
        public void CreateGrid()
        {
            mainGrid.ShowGridLines = false; // was true
            MainParent.ShowGridLines = false; // was true
            AppWindow.AuxGrid.ShowGridLines = false;

            //Clear grid
            mainGrid.Children.Clear();

            SetDockLocation();

            // Add highlight border
            if (Settings.Default.DockLocation <= 1)
            {
                GameHighlightBorder = new Border();
                mainGrid.Children.Add(GameHighlightBorder);
                Grid.SetColumn(GameHighlightBorder, 0);
                GameHighlightBorder.Background = GetGameHighlightBrush();
                GameHighlightBorder.Opacity = 0d;


            }
            else
            {
                GameHighlightBorder = new Border();
                mainGrid.Children.Add(GameHighlightBorder);
                Grid.SetRow(GameHighlightBorder, 0);
                GameHighlightBorder.Background = GetGameHighlightBrush();
                GameHighlightBorder.Opacity = 0d;
            }

            UpdateLayout();

            // Is the chat bar enabled?
            if (Settings.Default.ShowChatBar)
            {
                SetChatBarVisibility(true);
            }

            UpdateBackgroundSize();
            UpdateBackgroundVisuals();

            if (Settings.Default.LaunchOnStartup)
            {
                UniversalPlatform.Startup.UpdateStartupState(true);
            }

            UpdateBigAddButtonState();
            CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);

            Console.WriteLine("Grid CHILD count at start: " + AppWindow.mainGrid.Children.Count);
            Console.WriteLine("Grid CDEF count at start: " + AppWindow.mainGrid.ColumnDefinitions.Count);
        }

        private Image InitBigAddButton()
        {
            // Create the dummy Game
            Image gameButton = new Image();
            //gameButton.Name = "BigAddButton";

            gameButton.Width = Settings.Default.StartupIconSize;
            gameButton.Margin = DefaultGameMargins; //new Thickness(1.5, 5, 1.5, 5);

            // Set Style
            gameButton.Style = (Style)TryFindResource("GameButtonStyleStatic");
            RenderOptions.SetBitmapScalingMode(gameButton, BitmapScalingMode.HighQuality);

            // Set Context Menu
            gameButton.ContextMenu = Resources["DummyButtonContextMenu"] as ContextMenu;

            // Load image
            gameButton.Source = Icons.BitmapTools.ImageFromPath(AppDomain.CurrentDomain.BaseDirectory + @"Assets\emptyListButton.png");

            // Alignment
            gameButton.HorizontalAlignment = HorizontalAlignment.Center;
            gameButton.VerticalAlignment = VerticalAlignment.Center;

            // Add click handlers
            gameButton.MouseLeftButtonDown += AddGameDummyButton_MouseLeftButtonDown;

            return gameButton;
        }

        private void UpdateBigAddButtonState()
        {
            Console.WriteLine("Maingrid Children count: " + mainGrid.Children.Count);

            if (GameObjects.Count == 0) //&& mainGrid.Children[0].GetType().Name != "Image")
            {
                // Initialize it once if null
                if (BigAddButton == null)
                    BigAddButton = InitBigAddButton();

                if (Settings.Default.DockLocation <= 1)
                {
                    // Create the grid column
                    ColumnDefinition gridCol = new ColumnDefinition
                    {
                        Width = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto)
                    };
                    mainGrid.ColumnDefinitions.Add(gridCol);

                    // Place in grid
                    mainGrid.Children.Add(BigAddButton);
                    Grid.SetColumn(BigAddButton, 0);
                }
                else
                {
                    // Create the grid row
                    RowDefinition gridRow = new RowDefinition
                    {
                        Height = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto)
                    };
                    mainGrid.RowDefinitions.Add(gridRow);

                    // Place in grid
                    mainGrid.Children.Add(BigAddButton);
                    Grid.SetRow(BigAddButton, 0);
                }

                // Update Layout and re-center
                CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
            else // remove it if grid has games
            {
                mainGrid.Children.Remove(BigAddButton);
            }
        }

        public void SetChatBarVisibility(bool visible)
        {
            if (visible)
            {
                if (LauncherSpecific.Steam.Exists && LauncherSpecific.Discord.Exists) // are both discord and steam present?
                {
                    steamFriendsLaunchButton.Visibility = Visibility.Visible;
                    discordLaunchButton.Visibility = Visibility.Visible;

                    if (Settings.Default.DockLocation <= 1) // top bottom
                    {
                        Grid.SetColumn(steamFriendsLaunchButton, 0);
                        Grid.SetColumn(discordLaunchButton, 1);
                    }
                    else // left right
                    {
                        Grid.SetRow(steamFriendsLaunchButton, 0);
                        Grid.SetRow(discordLaunchButton, 1);
                    }
                }
                else
                {
                    if (LauncherSpecific.Steam.Exists)
                    {
                        steamFriendsLaunchButton.Visibility = Visibility.Visible;

                        if (Settings.Default.DockLocation <= 1)
                        {
                            steamFriendsLaunchButton.SetValue(Grid.ColumnSpanProperty, 2);
                            Grid.SetColumn(steamFriendsLaunchButton, 0);
                        }
                        else
                        {
                            steamFriendsLaunchButton.SetValue(Grid.RowSpanProperty, 2);
                            Grid.SetRow(steamFriendsLaunchButton, 0);
                        }

                    }

                    if (LauncherSpecific.Discord.Exists)
                    {
                        discordLaunchButton.Visibility = Visibility.Visible;

                        if (Settings.Default.DockLocation <= 1)
                        {
                            discordLaunchButton.SetValue(Grid.ColumnSpanProperty, 2);
                            Grid.SetColumn(discordLaunchButton, 0);
                        }
                        else
                        {
                            discordLaunchButton.SetValue(Grid.RowSpanProperty, 2);
                            Grid.SetRow(discordLaunchButton, 0);
                        }
                    }
                }

                SetChatBarPositionSizeMargin();
            }
            else
            {
                steamFriendsLaunchButton.Visibility = Visibility.Hidden;
                discordLaunchButton.Visibility = Visibility.Hidden;

                ChatGrid.Height = 0;
                ChatGrid.Width = 0;
                ChatGrid.Margin = new Thickness(0);
            }

            CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

        private void SetChatBarPositionSizeMargin()
        {
            if (Settings.Default.ShowChatBar)
            {
                switch (Settings.Default.DockLocation)
                {
                    case 0:
                        ChatGrid.Width = 70;
                        ChatGrid.Height = 25;
                        AppWindow.ChatGrid.HorizontalAlignment = HorizontalAlignment.Center;
                        ChatGrid.Margin = DefaultChatBarThickness;
                        break;
                    case 1:
                        ChatGrid.Width = 70;
                        ChatGrid.Height = 25;
                        AppWindow.ChatGrid.HorizontalAlignment = HorizontalAlignment.Center;
                        ChatGrid.Margin = DefaultChatBarThickness;
                        break;
                    case 2:
                        ChatGrid.Width = 25;
                        ChatGrid.Height = 70;
                        AppWindow.ChatGrid.HorizontalAlignment = HorizontalAlignment.Left;
                        ChatGrid.Margin = DefaultChatBarThickness;
                        break;
                    case 3:
                        ChatGrid.Width = 25;
                        ChatGrid.Height = 70;
                        AppWindow.ChatGrid.HorizontalAlignment = HorizontalAlignment.Right;
                        ChatGrid.Margin = DefaultChatBarThickness;
                        break;
                    default:
                        break;
                }
            }
        }

        private void AddGameDummyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if(e.ClickCount == Settings.Default.LaunchClickCount)
            //{

            AddGameManually();
            //}

        }

        private static void RefreshGrid(List<Game> overrideList = null, Image mglass = null)
        {
            if (overrideList == null)
            {
                for (int i = 0; i < GameObjects.Count; i++)
                {
                    GameObjects[i].Index = i;
                    Panel.SetZIndex(GameObjects[i], i);

                    if (Settings.Default.DockLocation <= 1)
                    {
                        Grid.SetColumn(GameObjects[i], i);
                        GameObjects[i].OwnedColumnDefinition = AppWindow.mainGrid.ColumnDefinitions[i];
                    }
                    else
                    {
                        Grid.SetRow(GameObjects[i], i);
                        GameObjects[i].OwnedRowDefinition = AppWindow.mainGrid.RowDefinitions[i];
                    }

                    KeyboardNavigation.SetTabIndex(GameObjects[i], i);
                }
            }
            else
            {
                if (mglass == null)
                {
                    for (int i = 0; i < overrideList.Count; i++)
                    {
                        overrideList[i].Index = i;
                        Panel.SetZIndex(overrideList[i], i);

                        if (Settings.Default.DockLocation <= 1)
                        {
                            Grid.SetColumn(overrideList[i], i);
                            GameObjects[i].OwnedColumnDefinition = AppWindow.mainGrid.ColumnDefinitions[i];
                        }
                        else
                        {
                            Grid.SetRow(overrideList[i], i);
                            GameObjects[i].OwnedRowDefinition = AppWindow.mainGrid.RowDefinitions[i];
                        }

                        KeyboardNavigation.SetTabIndex(overrideList[i], i);
                    }
                }
                else
                {
                    if (Settings.Default.DockLocation <= 1)
                        Grid.SetColumn(mglass, 0);
                    else
                        Grid.SetRow(mglass, 0);
                }
            }
        }

        private static void RecenterAfterGridAnimation()
        {
            for (int i = 0; i < GameObjects.Count; i++)
            {
                GameObjects[i].MoveTo(0, 0, 0);
            }

            // Fade in
            //for (int i = 0; i < gameObjects.Count; i++)
            //{
            //    gameObjects[i].BeginStoryboard(gameObjects[i].FadeInStoryboard);
            //}
        }

        public void AddGameToGrid(Game game)
        {
            if (allowDuplicates)
            {
                if (game != null) //&& game.IsManuallyRemoved == false
                {
                    // Create the Image
                    game.Width = Settings.Default.StartupIconSize;
                    game.Margin = DefaultGameMargins; //new Thickness(1.5, 5, 1.5, 5);

                    // Set Style
                    game.Style = (Style)TryFindResource("GameButtonStyleStatic"); //Resources["GameButtonStyleStatic"] as Style;
                    RenderOptions.SetBitmapScalingMode(game, BitmapScalingMode.Fant);

                    // Set max height
                    game.MaxHeight = Settings.Default.StartupIconSize;
                    //mainGrid.MaxHeight = Settings.Default.StartupIconSize + 3 + 10;

                    // Set Context Menu
                    game.ContextMenu = Resources["GlobalContextMenu"] as ContextMenu;

                    // Load image
                    game.Source = IconManager.LoadGameIcon(game);

                    // Alignment
                    game.HorizontalAlignment = HorizontalAlignment.Center;
                    game.VerticalAlignment = VerticalAlignment.Center;

                    // Stretch type
                    game.Stretch = Stretch.UniformToFill;
                    //game.StretchDirection = StretchDirection.DownOnly;

                    // Set Focusable and set Keyboard Navigation in refresh grid
                    game.Focusable = true;
                    game.FocusVisualStyle = null;
                    game.GotFocus += Game_GotFocus;
                    game.MouseEnter += Game_MouseEnter;
                    game.MouseLeave += Game_MouseLeave;

                    // Add click handlers
                    game.MouseDown += Game_MouseDown;
                    game.MouseUp += Game_MouseUp;
                    game.MouseRightButtonDown += new MouseButtonEventHandler(Game_RightClick);
                    game.ToolTipOpening += Game_ToolTipOpening;

                    //// Add touch handlers // unused
                    //game.TouchDown += Game_TouchDown;
                    //game.TouchUp += Game_TouchUp;

                    // Block requestBringIntoView to disable Scrollviewer native scrolling
                    // and use Smooth Scroll instead
                    game.RequestBringIntoView += Game_RequestBringIntoView;

                    if (IsDockHorizontal)
                    {
                        // Create the grid column
                        ColumnDefinition gridCol = new ColumnDefinition
                        {
                            Width = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto),
                            Tag = game.Index
                        };

                        game.OwnedColumnDefinition = gridCol;
                        mainGrid.ColumnDefinitions.Add(gridCol);

                        // Remove dummy button if it's there
                        if (mainGrid.Children[0].GetType().Name == "Image")
                        {
                            //Console.WriteLine("Removing dummy button");
                            mainGrid.Children.RemoveAt(0);
                            mainGrid.ColumnDefinitions.RemoveAt(0);
                        }

                        // Place in grid
                        mainGrid.Children.Add(game);
                        Grid.SetColumn(game, game.Index);
                    }
                    else
                    {
                        // Create the grid row
                        RowDefinition gridRow = new RowDefinition
                        {
                            Height = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto),
                            Tag = game.Index
                        };

                        game.OwnedRowDefinition = gridRow;
                        mainGrid.RowDefinitions.Add(gridRow);

                        // Remove dummy button if it's there
                        if (mainGrid.Children[0].GetType().Name == "Image")
                        {
                            //Console.WriteLine("Removing dummy button");
                            mainGrid.Children.RemoveAt(0);
                            mainGrid.RowDefinitions.RemoveAt(0);
                        }

                        // Place in grid
                        mainGrid.Children.Add(game);
                        Grid.SetRow(game, game.Index);
                    }

                    // Add tooltip
                    //UpdateGameTooltip(game);

                    // Fix Dock Name
                    if (game.DockName.Length == 0)
                        game.DockName = game.DisplayName;

                    // Scroll to end
                    Dock.Scrolling.ScrollTo(Dock.Scrolling.ScrollableMax() + ActualGameSize/2d - 5d);

                    // Update Layout and re-center
                    RefreshGrid();
                    CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
                    UpdateSettings();
                    UpdateIconMargins(); // Fix first/last icon margins to remove gaps
                    UpdateBigAddButtonState();
                }
            }
            else
            {
#pragma warning disable CS0162 // Unreachable code detected
                if (game != null && !game.IsDuplicateGame())
#pragma warning restore CS0162 // Unreachable code detected
                {
                    // Create the grid column
                    ColumnDefinition gridCol = new ColumnDefinition
                    {
                        Width = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto)
                    };
                    mainGrid.ColumnDefinitions.Add(gridCol);

                    // Create the Image
                    //Image gameButton = new Image();
                    game.Width = Settings.Default.StartupIconSize;
                    game.Margin = new Thickness(1.5, 5, 1.5, 5);

                    // Set Style
                    game.Style = (Style)TryFindResource("GameButtonStyleStatic"); //Resources["GameButtonStyleStatic"] as Style;
                    RenderOptions.SetBitmapScalingMode(game, BitmapScalingMode.HighQuality);

                    // Set Context Menu
                    game.ContextMenu = Resources["GlobalContextMenu"] as ContextMenu;

                    // Load image
                    game.Source = Icons.IconManager.LoadGameIcon(game);

                    // Alignment
                    game.HorizontalAlignment = HorizontalAlignment.Center;
                    game.VerticalAlignment = VerticalAlignment.Top;


                    // Add click handlers
                    game.MouseDown += Game_MouseDown;
                    game.MouseRightButtonDown += new MouseButtonEventHandler(Game_RightClick);

                    // Place in grid
                    if (/*game.Index == 0 */mainGrid.Children[0].GetType().Name == "Image")
                    {
                        //Console.WriteLine("Removing dummy button");
                        mainGrid.Children.RemoveAt(0);
                    }
                    mainGrid.Children.Add(game);
                    Grid.SetColumn(game, game.Index);

                    // Update Layout and re-center
                    RefreshGrid();
                    CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
                    UpdateSettings();
                }

            }
        }

        private void Game_MouseLeave(object sender, MouseEventArgs e)
        {
            if (LastInputSource == InputSource.Mouse)
            {
                GameTooltip.IsOpen = false;
            }
        }

        private void Game_MouseEnter(object sender, MouseEventArgs e)
        {
            if (LastInputSource == InputSource.Mouse)
            {
                UpdateGameTooltip((Game)sender);
            }
        }

        private void Game_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            var game = sender as Game;
            var ttip = (ToolTip)game.ToolTip;

            // Reposition depending on width/height
            switch (Settings.Default.DockLocation)
            {
                case 0:
                    ToolTipService.SetHorizontalOffset(game, Settings.Default.StartupIconSize / 2d - ttip.ActualWidth / 2d);
                    break;
                case 1:
                    ToolTipService.SetHorizontalOffset(game, Settings.Default.StartupIconSize / 2d - ttip.ActualWidth / 2d);
                    break;
                case 2:
                    ToolTipService.SetVerticalOffset(game, Settings.Default.StartupIconSize / 2d - ttip.ActualHeight / 2d); // 15 is tooltip height/2 from style in app.xaml
                    break;
                case 3:
                    ToolTipService.SetVerticalOffset(game, Settings.Default.StartupIconSize / 2d - ttip.ActualHeight / 2d); // 15 is tooltip height/2 from style in app.xaml
                    break;
                default:
                    break;
            }

            Console.WriteLine("Tooltip actual width " + ttip.ActualWidth);
            Console.WriteLine("Startup icon size " + Settings.Default.StartupIconSize);
        }

        public static Popup GameTooltip { get; private set; }
        public static TextBlock GameTooltipTextBlock { get; private set; }

        private const double gameToolTipWidth = 160d;
        private const double gameToolTipHeight = 26d;
        public static void CreateGameTooltip()
        {
            Popup pop = new Popup
            {
                AllowsTransparency = true
            };

            Grid popGrid = new Grid();

            Border popupBorder = new Border()
            {
                Width = gameToolTipWidth,
                Height = gameToolTipHeight,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E2E2E")),
                CornerRadius = new CornerRadius(8d),
                BorderThickness = new Thickness(1d),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF696969")) //Dim Gray
            };

            TextBlock txtBlock = new TextBlock
            {
                Width = gameToolTipWidth,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                MaxWidth = Settings.Default.StartupIconSize,
                FontFamily = new FontFamily("Lato Light"),
                FontSize = 14,
                Margin = new Thickness(10, 0, 10, 0)
            };

            GameTooltipTextBlock = txtBlock;

            _ = popGrid.Children.Add(popupBorder);
            _ = popGrid.Children.Add(txtBlock);

            pop.Child = popGrid;

            GameTooltip = pop;
        }

        public static void UpdateGameTooltip(Game game)
        {
            GameTooltip.IsOpen = false;

            if (Settings.Default.ShowGameLabels == false)
                return;

            GameTooltip.PlacementTarget = game;
            GameTooltipTextBlock.Text = game?.DockName;

            // Position
            switch (Settings.Default.DockLocation)
            {
                case 0:
                    GameTooltip.Placement = PlacementMode.Bottom;
                    GameTooltip.HorizontalOffset = (Settings.Default.StartupIconSize / 2d) - (gameToolTipWidth / 2d) + DefaultGameMargins.Left;
                    GameTooltip.VerticalOffset = 5d;
                    break;
                case 1:
                    GameTooltip.Placement = PlacementMode.Top;
                    GameTooltip.HorizontalOffset = (Settings.Default.StartupIconSize / 2d) - (gameToolTipWidth / 2d) + DefaultGameMargins.Left;
                    GameTooltip.VerticalOffset = -5d;
                    break;
                case 2:
                    GameTooltip.Placement = PlacementMode.Right;
                    GameTooltip.HorizontalOffset = 8d;
                    GameTooltip.VerticalOffset = (Settings.Default.StartupIconSize / 2d) - (gameToolTipHeight / 2d) + DefaultGameMargins.Top;
                    break;
                case 3:
                    GameTooltip.Placement = PlacementMode.Left;
                    GameTooltip.HorizontalOffset = -8d;
                    GameTooltip.VerticalOffset = (Settings.Default.StartupIconSize / 2d) - (gameToolTipHeight / 2d) + DefaultGameMargins.Top;
                    break;
                default:
                    break;
            }

            GameTooltip.IsOpen = true;

        }

        private void Game_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            //Console.WriteLine("Game requested bring into view, cancelling");
            e.Handled = true;
        }

        private void Game_GotFocus(object sender, RoutedEventArgs e)
        {
            Game game = sender as Game;

            Console.WriteLine("Focused game: " + game.DisplayName + " last input source: " + LastInputSource);
            //Console.WriteLine("Original source " + e.OriginalSource);
            //Console.WriteLine("Source " + e.Source);
            //Console.WriteLine("Owner type full name " + e.RoutedEvent.OwnerType.FullName);
            //Console.WriteLine("Routed event name " + e.RoutedEvent.Name);
            //Console.WriteLine("Handler type full name " + e.RoutedEvent.HandlerType.FullName);

            if (game != null)
            {
                if (Settings.Default.DockLocation <= 1)
                {
                    Grid.SetColumn(AppWindow.GameHighlightBorder, game.Index);
                }
                else
                {
                    Grid.SetRow(AppWindow.GameHighlightBorder, game.Index);
                    Console.WriteLine("Highlighted game index is: " + game.Index);
                }

                AnimateHighlight(true);
                HighlightedGame = game;

                if (LastInputSource != InputSource.Mouse)
                    ScrollToHighlightedGame();
            }
        }

        /// <summary>
        /// Calculates how much to scroll by using mainGrid.Children.Count
        /// </summary>
        static void ScrollToHighlightedGame()
        {
            UpdateGameTooltip(HighlightedGame);

            if (AppWindow.GridScrollViewer.ScrollableHeight == 0 && AppWindow.GridScrollViewer.ScrollableWidth == 0)
                return;
            // Scroll to game when using ArrowNav
            //SetSmoothScrollTarget(HighlightedGame.Index * (1d / (AppWindow.mainGrid.Children.Count - 2d))); // -1 for highlight child, -1d for starting from index 0
            Dock.Scrolling.ScrollTo(HighlightedGame);
            Console.WriteLine("Grid children count is: " + AppWindow.mainGrid.Children.Count);
            Console.WriteLine("Game count is: " + GameObjects.Count);
        }


        /// <summary>
        /// Refreshes grid, updates icon size, centers window, updates settings and adds Add icon if no items remain in grid.
        /// Need to set it up to remove saved icons and any other game residual.
        /// </summary>
        public void CleanupAfterRemoval()
        {
            RefreshGrid();

            //CenterWindowOnScreen(); // causes jitter when removing some icons
            UpdateSettings();

            if (GameObjects.Count == 0)
            {
                UpdateBigAddButtonState();
            }
            else
            {
                RecenterAfterGridAnimation();
            }
        }

        public enum GamepadStatus { None, Connected, Disconnected }

        // finish zoom icon sizes
        // finish controller repeated hold of analog
        public void UpdateGameIconSizes(GamepadStatus gStatus = GamepadStatus.None)
        {

            if (gStatus == GamepadStatus.Connected)
            {
                IsZoomActive = true;
                Settings.Default.StartupIconSize = Settings.Default.ZoomIconSize;
            }

            if (gStatus == GamepadStatus.Disconnected)
            {
                IsZoomActive = false;
                Settings.Default.StartupIconSize = Settings.Default.OriginalIconSize;
            }

            ToggleScrollViewerEdgeFade();
            //ToggleScrollViewerOffsets();

            GameHighlightBorder.Width = Settings.Default.StartupIconSize;

            Console.WriteLine("New icon size is: " + Settings.Default.StartupIconSize);

            // Needs to be finished but it'll take some time
            //if (Settings.Default.DockLocation <= 1)
            //    mainGrid.MaxHeight = Settings.Default.StartupIconSize + 3 + 10;
            //else
            //    mainGrid.MaxWidth = Settings.Default.StartupIconSize + 3 + 10;

            foreach (Game g in GameObjects) // move to game object initializer
            {
                //g.AnimateIconSize();
                g.Width = Settings.Default.StartupIconSize;
                //g.Height = Settings.Default.StartupIconSize;
                //g.MaxWidth = Settings.Default.StartupIconSize;
                g.MaxHeight = Settings.Default.StartupIconSize;
            }

            if (BigAddButton != null)
            {
                BigAddButton.Width = Settings.Default.StartupIconSize;
                BigAddButton.MaxHeight = Settings.Default.StartupIconSize;
            }

            //Console.WriteLine("SV vertical offset "+GridScrollViewer.VerticalOffset);
            //Console.WriteLine("SV scrollable height "+GridScrollViewer.ScrollableHeight);
            //Console.WriteLine("MEDIATOR vertical offset "+Mediator.VerticalOffset);
            //Console.WriteLine("MEDIATOR scrollable height multiplier " + Mediator.ScrollableHeightMultiplier);
            //Console.WriteLine("SVLerper " + scrollViewerLerper);
            //Console.WriteLine("SSTarget  " + SmoothScrollTarget);

            //if(Settings.Default.DockLocation <= 1)
            //{
            //    // adjust scroll on icon size change
            //    if (IsDockPerfectlyFittingScreen() == false)
            //    {
            //        if (GridScrollViewer.ScrollableWidth != 0)
            //        {
            //            double adjustedTarget = GridScrollViewer.HorizontalOffset / GridScrollViewer.ScrollableWidth;
            //            scrollViewerLerper = adjustedTarget;
            //            SetSmoothScrollTarget(adjustedTarget);
            //        }
            //    }
            //    else
            //    {
            //        scrollViewerLerper = 0.5d;
            //        SetSmoothScrollTarget(0.5d);
            //    }
            //    Console.WriteLine("SVLerper after change " + scrollViewerLerper);
            //}
            //else
            //{
            //    // adjust scroll on icon size change
            //    if (IsDockPerfectlyFittingScreen() == false)
            //    {
            //        // throws NaN exception if it's 0
            //        if(GridScrollViewer.ScrollableHeight != 0)
            //        {
            //            double adjustedTarget = GridScrollViewer.VerticalOffset / GridScrollViewer.ScrollableHeight;
            //            scrollViewerLerper = adjustedTarget;
            //            SetSmoothScrollTarget(adjustedTarget);
            //        }

            //    }
            //    else
            //    {
            //        scrollViewerLerper = 0.5d;
            //        SetSmoothScrollTarget(0.5d);
            //    }
            //    Console.WriteLine("SVLerper after change " + scrollViewerLerper);
            //}

            //KeepLerperStill();

            CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

      

        #endregion

        public enum GameActionType { Add, Remove }

        static double GameMoveDist = ActualGameSize;
        static double NegativeGameMoveDist => -ActualGameSize;
        static double HalfGameMoveDist => ActualGameSize / 2d;
        static double NegativeHalfGameMoveDist => -ActualGameSize / 2d;

        public static bool IsHittingRightBottomEdgeOnRemove
        {
            get
            {
                if (IsDockHorizontal)
                {
                    if(AppWindow.GridScrollViewer.HorizontalOffset + ActualGameSize >= Scrolling.ScrollableMax())
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (AppWindow.GridScrollViewer.VerticalOffset + ActualGameSize >= Scrolling.ScrollableMax())
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

            }
        }

        public static bool IsHittingLeftTopEdgeOnRemove
        {
            get
            {
                if (IsDockHorizontal)
                {
                    if (AppWindow.GridScrollViewer.HorizontalOffset - ActualGameSize <= 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (AppWindow.GridScrollViewer.VerticalOffset - ActualGameSize <= 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

            }
        }
        /// <summary>
        /// Always move right to left because the grid will always recenter with left corner first!
        /// </summary>
        /// <param name="game"></param>
        /// <param name="gameActionType"></param>
        public static void AnimateGridCellsZoomed(Game game, GameActionType gameActionType)
        {
            try
            {
                if (IsDockHorizontal)
                {
                    if (game != null && gameActionType == GameActionType.Remove)
                    {
                        // Start animating column shrink
                        //AnimateGridColumn(game.Index);

                        // First game is being removed
                        // at least 2 objects necessary for animation
                        if (game.Index == 0 && GameObjects.Count > 1)
                        {
                            Console.WriteLine("Animating First Cell");
                            for (int i = 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(NegativeGameMoveDist, 0, gridAnimationLength);
                            }
                        }

                        //// Last game is being removed
                        //// at least 2 objects necessary for animation
                        //if (game.Index == GameObjects.Count - 1 && GameObjects.Count > 1)
                        //{
                        //    Console.WriteLine("Animating Last Cell");
                        //    for (int i = GameObjects.Count - 2; i >= 0; i--)
                        //    {
                        //        GameObjects[i].MoveTo(GameMoveDist, 0, gridAnimationLength);
                        //    }

                        //}

                        // Game in middle is being removed
                        // at least 3 objects necessary for animation
                        if (game.Index > 0 && game.Index < GameObjects.Count - 1 && GameObjects.Count > 2)
                        {
                            Console.WriteLine("Animating Middle Cell");

                            // move all games from the right to left, cause left aligned grid
                            for (int i = game.Index + 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(NegativeGameMoveDist, 0, gridAnimationLength);
                            }

                            //for (int i = game.Index - 1; i >= 0; i--)
                            //{
                            //    GameObjects[i].MoveTo(GameObjects[i].ActualWidth / 2, 0, gridAnimationLength);
                            //}
                        }
                    }

                    // Obsolete code
                    if (gameActionType == GameActionType.Add)
                    {
                        for (int i = 0; i < GameObjects.Count - 1; i++)
                        {
                            GameObjects[i].MoveTo(NegativeGameMoveDist, 0, 300);
                        }
                    }
                }
                else // vertical
                {
                    if (game != null && gameActionType == GameActionType.Remove)
                    {
                        // Start animating row shrink
                        //AnimateGridRow(game.Index);

                        // First game is being removed
                        // at least 2 objects necessary for animation
                        if (game.Index == 0 && GameObjects.Count > 1)
                        {
                            Console.WriteLine("Animating First Cell");
                            for (int i = 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(0, NegativeGameMoveDist, gridAnimationLength);
                            }
                        }

                        //// Last game is being removed
                        //// at least 2 objects necessary for animation
                        //if (game.Index == GameObjects.Count - 1 && GameObjects.Count > 1)
                        //{
                        //    Console.WriteLine("Animating Last Cell");
                        //    for (int i = GameObjects.Count - 2; i >= 0; i--)
                        //    {
                        //        GameObjects[i].MoveTo(0, GameMoveDist, gridAnimationLength);
                        //    }
                        //}

                        // Game in middle is being removed
                        // at least 3 objects necessary for animation
                        if (game.Index > 0 && game.Index < GameObjects.Count - 1 && GameObjects.Count > 2)
                        {
                            Console.WriteLine("Animating Middle Cell");
                            // move all games from the bottom to top, cause top aligned grid
                            for (int i = game.Index + 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(0, NegativeGameMoveDist, gridAnimationLength);
                            }
                        }
                    }

                    // Obsolete code
                    if (gameActionType == GameActionType.Add)
                    {
                        for (int i = 0; i < GameObjects.Count - 1; i++)
                        {
                            GameObjects[i].MoveTo(0, GameObjects[i].ActualHeight / -2, gridAnimationLength);
                        }
                    }
                }

            }
            catch (Exception)
            {
                OpenWindow.Notification("Error while animating grid");
                //throw;
            }
        }

        // ORIGINAL GRID ANIMATE CELLS, WORKS although jittery
        /// <summary>
        /// Always move right to left because the grid will always recenter with left corner first!
        /// </summary>
        /// <param name="game"></param>
        /// <param name="gameActionType"></param>
        public static void AnimateGridCells(Game game, GameActionType gameActionType)
        {
            try
            {
                if (IsDockHorizontal)
                {
                    if (game != null && gameActionType == GameActionType.Remove)
                    {
                        // Start animating column shrink
                        //AnimateGridColumn(game.Index);

                        // First game is being removed
                        // at least 2 objects necessary for animation
                        if (game.Index == 0 && GameObjects.Count > 1)
                        {
                            Console.WriteLine("Animating First Cell");
                            for (int i = 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(NegativeHalfGameMoveDist, 0, gridAnimationLength);
                            }
                        }

                        // Last game is being removed
                        // at least 2 objects necessary for animation
                        if (game.Index == GameObjects.Count - 1 && GameObjects.Count > 1)
                        {
                            Console.WriteLine("Animating Last Cell");
                            for (int i = GameObjects.Count - 2; i >= 0; i--)
                            {
                                GameObjects[i].MoveTo(HalfGameMoveDist, 0, gridAnimationLength);
                            }
                        }

                        // Game in middle is being removed
                        // at least 3 objects necessary for animation
                        if (game.Index > 0 && game.Index < GameObjects.Count - 1 && GameObjects.Count > 2)
                        {
                            Console.WriteLine("Animating Middle Cell");
                            for (int i = game.Index + 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(NegativeHalfGameMoveDist, 0, gridAnimationLength);
                            }

                            for (int i = game.Index - 1; i >= 0; i--)
                            {
                                GameObjects[i].MoveTo(HalfGameMoveDist, 0, gridAnimationLength);
                            }
                        }
                    }

                    // Obsolete code
                    if (gameActionType == GameActionType.Add)
                    {
                        for (int i = 0; i < GameObjects.Count - 1; i++)
                        {
                            GameObjects[i].MoveTo(ActualGameSize / -2, 0, gridAnimationLength);
                        }
                    }
                }
                else // vertical
                {
                    if (game != null && gameActionType == GameActionType.Remove)
                    {
                        // Start animating row shrink
                        //AnimateGridRow(game.Index);

                        // First game is being removed
                        // at least 2 objects necessary for animation
                        if (game.Index == 0 && GameObjects.Count > 1)
                        {
                            Console.WriteLine("Animating First Cell");
                            for (int i = 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(0, NegativeHalfGameMoveDist, gridAnimationLength);
                            }
                        }

                        // Last game is being removed
                        // at least 2 objects necessary for animation
                        if (game.Index == GameObjects.Count - 1 && GameObjects.Count > 1)
                        {
                            Console.WriteLine("Animating Last Cell");
                            for (int i = GameObjects.Count - 2; i >= 0; i--)
                            {
                                GameObjects[i].MoveTo(0, HalfGameMoveDist, gridAnimationLength);
                            }
                        }

                        // Game in middle is being removed
                        // at least 3 objects necessary for animation
                        if (game.Index > 0 && game.Index < GameObjects.Count - 1 && GameObjects.Count > 2)
                        {
                            Console.WriteLine("Animating Middle Cell");
                            for (int i = game.Index + 1; i < GameObjects.Count; i++)
                            {
                                GameObjects[i].MoveTo(0, NegativeHalfGameMoveDist, gridAnimationLength);
                            }

                            for (int i = game.Index - 1; i >= 0; i--)
                            {
                                GameObjects[i].MoveTo(0, HalfGameMoveDist, gridAnimationLength);
                            }
                        }
                    }

                    if (game != null && gameActionType == GameActionType.Add)
                    {
                        for (int i = 0; i < GameObjects.Count - 1; i++)
                        {
                            GameObjects[i].MoveTo(0, -ActualGameSize, gridAnimationLength);
                        }
                    }
                }

            }
            catch (Exception)
            {
                OpenWindow.Notification("Error while animating grid");
                //throw;
            }
        }

        static GridLengthAnimation gridCellAnimation = GetGridCellAnimation();
        static GridLengthAnimation GetGridCellAnimation()
        {
            GridLengthAnimation gla = new GridLengthAnimation();
            gla.Completed += GridCellAnimation_Completed;
            return gla;
        }

        public static void AnimateGridColumn(int index)
        {
            gridCellAnimation.From = new GridLength(AppWindow.mainGrid.ColumnDefinitions[index].ActualWidth, GridUnitType.Pixel);
            gridCellAnimation.To = new GridLength(0, GridUnitType.Pixel);
            gridCellAnimation.Duration = new TimeSpan(0, 0, 0, gridAnimationLength);
            AppWindow.mainGrid.ColumnDefinitions[index].BeginAnimation(
            ColumnDefinition.WidthProperty, gridCellAnimation);
        }

        public static void AnimateGridRow(int index)
        {
            gridCellAnimation.From = new GridLength(AppWindow.mainGrid.RowDefinitions[index].ActualHeight, GridUnitType.Pixel);
            gridCellAnimation.To = new GridLength(0, GridUnitType.Pixel);
            gridCellAnimation.Duration = new TimeSpan(0, 0, 0, gridAnimationLength);
            AppWindow.mainGrid.RowDefinitions[index].BeginAnimation(
            RowDefinition.HeightProperty, gridCellAnimation);
        }

        //public static void AnimateWholeGrid(int index)
        //{
        //    gridCellAnimation.From = new GridLength(ScrollViewer.actua, GridUnitType.Pixel);
        //    gridCellAnimation.To = new GridLength(AppWindow.mainGrid.ActualWidth - AppWindow.mainGrid.ColumnDefinitions[index].ActualWidth, GridUnitType.Pixel);
        //    gridCellAnimation.Duration = new TimeSpan(0, 0, 0, gridAnimationLength);
        //    AppWindow.mainGrid.BeginAnimation(
        //    Grid.WidthProperty, gridCellAnimation);
        //}

        private static void GridCellAnimation_Completed(object sender, EventArgs e)
        {
            //Console.WriteLine("GRID CELL ANIM COMPLETE");
            //// Cleans up rows and columns after remove animation is finished
            //// was in shrink storyboard of game class
            //AppWindow.mainGrid.Children.Remove(RightClickedGame);

            //if (Properties.Settings.Default.DockLocation <= 1)
            //    AppWindow.mainGrid.ColumnDefinitions.Remove(RightClickedGame.OwnedColumnDefinition);
            //else
            //    AppWindow.mainGrid.RowDefinitions.Remove(RightClickedGame.OwnedRowDefinition);

            //Program prog = ProgramObjects.FirstOrDefault(p => p.IconGUID == RightClickedGame.IconGUID);

            //GameObjects.Remove(RightClickedGame);

            //// remove the program so next rescan can restore initial games list, this needs to be upgraded
            //// to something more modular and less request intensive

            //if (prog != null && prog.IsGame)
            //{
            //    Console.WriteLine("Removed from program list: " + prog.DisplayName + " with GUID " + prog.IconGUID);
            //    ProgramObjects.Remove(prog);
            //}

            //AppWindow.CleanupAfterRemoval();
        }

        #region Left and Right Clicks
        private void Game_MouseUp(object sender, MouseButtonEventArgs e)
        {
            LastInputSource = InputSource.Mouse;

            if (!isRearrangingGrid)
            {
                Game game = sender as Game;

                //if (e.ChangedButton == MouseButton.Left && e.ClickCount == Settings.Default.LaunchClickCount)
                //{
                //    LaunchGame(game);
                //}

                //if (e.ChangedButton == MouseButton.Middle && e.ClickCount == Settings.Default.LaunchClickCount)
                //{
                //    LaunchGame(game, true);
                //}

                if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1 && Settings.Default.LaunchClickCount == 2)
                {
                    game.Focus();
                    AppWindow.AnimateHighlight(true);
                }
            }

        }

        private static void Game_MouseDown(object sender, MouseButtonEventArgs e)
        {
            #region Original Code
            //Game game = sender as Game;

            //if (e.ChangedButton == MouseButton.Left && e.ClickCount == Settings.Default.LaunchClickCount)
            //{
            //    LaunchGame(game);
            //}

            //if (e.ChangedButton == MouseButton.Middle && e.ClickCount == Settings.Default.LaunchClickCount)
            //{
            //    LaunchGame(game, true);
            //}

            //if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1 && Settings.Default.LaunchClickCount == 2)
            //{
            //    game.Focus();
            //    AppWindow.AnimateHighlight(true);
            //}
            #endregion

            Game game = sender as Game;

            if (!isRearrangingGrid)
            {
                if (e.ChangedButton == MouseButton.Left && e.ClickCount == Settings.Default.LaunchClickCount)
                {
                    LaunchGame(game);
                }

                if (e.ChangedButton == MouseButton.Middle && e.ClickCount == Settings.Default.LaunchClickCount)
                {
                    LaunchGame(game, true);
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // set the game to grab
                leftMouseDownOverGame = true;
                draggedGame = game;
            }
        }
        static bool leftMouseDownOverGame;
        static bool isRearrangingGrid;
        static Game draggedGame;

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!Settings.Default.IsGridLocked
                && !IsSearchBoxExpanded
                && e.LeftButton == MouseButtonState.Pressed)
            {
                // Re-enable for grid rearranging
                StartDraggingGame();
            }

            if (isRearrangingGrid)
            {
                TransformGroup tg = draggedGame.RenderTransform as TransformGroup;
                TranslateTransform trans = tg.Children[1] as TranslateTransform;

                var mousePosRelativeToGame = e.GetPosition(draggedGame);
                double newGamePos;

                if (Settings.Default.DockLocation <= 1)
                {
                    newGamePos = mousePosRelativeToGame.X + trans.X - draggedGame.ActualWidth / 2d;
                    trans.X = newGamePos;
                }
                else
                {
                    newGamePos = mousePosRelativeToGame.Y + trans.Y - draggedGame.ActualHeight / 2d;
                    trans.Y = newGamePos;
                }

                CalculateDraggedGameCurrentGridPosition(e.GetPosition(AppWindow));
            }
        }

        private void mainWindow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            StopDraggingGame();
        }

        private void mainWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            StopDraggingGame();
        }

        static void StopDraggingGame()
        {
            if (isRearrangingGrid)
            {
                // stop dragging if mouse up over window - make it global
                leftMouseDownOverGame = false;
                isRearrangingGrid = false;

                Console.WriteLine("Stopped Dragging Game");

                // Rearrange Grid
                RearrangeGridOnDragStop();
            }
        }

        // Don't swap, nudge everything to direction
        static void RearrangeGridOnDragStop()
        {
            Console.WriteLine("Last nudged index is (Rearrange grid) " + lastNudgedIndex);
            int fromIndex = draggedGame.Index;
            int toIndex = lastNudgedIndex + 1;

            Console.WriteLine("To index when rearranging grid is : " + toIndex);
            Console.WriteLine("Dragged game index is " + draggedGame.Index);
            Console.WriteLine("Dragged game -1 " + (draggedGame.Index - 1));
            // game has been moved
            if (lastNudgedIndex != draggedGame.Index - 1)
            {
                if (draggedGame.Index == 0) // is first
                {
                    // Mirror gameobject list
                    var overrideList = GameObjects.ToList();

                    for (int i = lastNudgedIndex + 1; i > 0; i--)
                    {
                        MoveGameToNewGridPosition(i, i - 1, overrideList);
                    }

                    // Update gameobjects
                    GameObjects = overrideList.ToList();

                    SetDraggedGamePositionToLastNudgedIndex();
                }
                else if (draggedGame.Index == GameObjects.Count - 1) // is last
                {
                    // Mirror gameobject list
                    var overrideList = GameObjects.ToList();

                    for (int i = lastNudgedIndex + 1; i < GameObjects.Count - 1; i++)
                    {
                        MoveGameToNewGridPosition(i, i + 1, overrideList);
                    }

                    // Update gameobjects
                    GameObjects = overrideList.ToList();

                    SetDraggedGamePositionToLastNudgedIndex();
                }
                else // is in middle
                {
                    if (draggedGame.Index - 1 < lastNudgedIndex) // arrange to left
                    {
                        // Mirror gameobject list
                        var overrideList = GameObjects.ToList();

                        for (int i = lastNudgedIndex + 1; i > draggedGame.Index; i--)
                        {
                            MoveGameToNewGridPosition(i, i - 1, overrideList);
                        }

                        // Update gameobjects
                        GameObjects = overrideList.ToList();

                        SetDraggedGamePositionToLastNudgedIndex();

                        Console.WriteLine("Nudging to left");
                    }

                    if (draggedGame.Index - 1 > lastNudgedIndex) // arrange to right
                    {
                        // Mirror gameobject list
                        var overrideList = GameObjects.ToList();

                        for (int i = lastNudgedIndex + 1; i < draggedGame.Index; i++)
                        {
                            MoveGameToNewGridPosition(i, i + 1, overrideList);
                        }

                        // Update gameobjects
                        GameObjects = overrideList.ToList();

                        SetDraggedGamePositionToLastNudgedIndex();
                    }
                }

                SolidifyNewGamePositions();
            }
            else // return game to its original place and do nothing to the grid
            {
                draggedGame.MoveTo(0, 0, 0, false, true);
            }

            // Set zindex to its new default zindex
            Panel.SetZIndex(draggedGame, draggedGame.Index);

            lastNudgedIndex = -100;
        }

        static void MoveGameToNewGridPosition(int fromIndex, int toIndex, List<Game> overrideList)
        {
            // Set moved game
            var movedGame = GameObjects[fromIndex];

            // Move the dragged game
            movedGame.Index = toIndex;

            if (Settings.Default.DockLocation <= 1)
            {
                // Swap owned column definitions
                movedGame.OwnedColumnDefinition = AppWindow.mainGrid.ColumnDefinitions[toIndex];
            }
            else
            {
                // Swap owned row definitions
                movedGame.OwnedRowDefinition = AppWindow.mainGrid.RowDefinitions[toIndex];
            }

            // Swap in override list
            overrideList[toIndex] = movedGame;

        }

        static void SetDraggedGamePositionToLastNudgedIndex()
        {
            // Set index
            draggedGame.Index = lastNudgedIndex + 1;

            if (Settings.Default.DockLocation <= 1)
            {
                // Set owned column definitions
                draggedGame.OwnedColumnDefinition = AppWindow.mainGrid.ColumnDefinitions[lastNudgedIndex + 1];
            }
            else
            {
                // Set owned column definitions
                draggedGame.OwnedRowDefinition = AppWindow.mainGrid.RowDefinitions[lastNudgedIndex + 1];
            }

            // Update gameobjects
            GameObjects[lastNudgedIndex + 1] = draggedGame;
        }

        static void SolidifyNewGamePositions()
        {
            // Update Layout and re-center
            RefreshGrid();
            CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
            //UpdateSettings();

            // Reset positions
            Console.WriteLine("Rearranged Grid Names Start");
            for (int i = 0; i < GameObjects.Count; i++)
            {
                Console.WriteLine($"Game at index {i} name {GameObjects[i].DisplayName}");
                TransformGroup tg = GameObjects[i].RenderTransform as TransformGroup;
                TranslateTransform trans = tg.Children[1] as TranslateTransform;
                trans.X = 0;
                trans.Y = 0;
                Console.WriteLine($"with position X {trans.X}");
                Console.WriteLine($"with position Y {trans.Y}");
            }

            // Save setup so it can be loaded
            SettingsManager.GeneralSettings.UpdateSettings();
        }

        static List<Game> dragSortedGames;

        static void StartDraggingGame()
        {
            // Triggers once when starting to drag a game
            if (leftMouseDownOverGame && !isRearrangingGrid)
            {
                // Remove game highlight
                AppWindow.AnimateHighlight(false);

                // Place on top of other games
                Panel.SetZIndex(draggedGame, GameObjects.Count + 1);

                // Sort games to exclude draggedGame
                dragSortedGames = new List<Game>();

                for (int i = 0; i < GameObjects.Count; i++)
                {
                    if (GameObjects[i] != draggedGame)
                    {
                        dragSortedGames.Add(GameObjects[i]);
                    }
                }

                if (Settings.Default.DockLocation <= 1)
                {
                    // don't nudge if first or last
                    if (draggedGame.Index != 0 && draggedGame.Index != GameObjects.Count - 1)
                    {
                        // Nudge Inwards once
                        // nudge right
                        for (int i = draggedGame.Index - 1; i >= 0; i--)
                        {
                            //if (gameObjects[i] != draggedGame)
                            dragSortedGames[i].MoveToAdditive(dragSortedGames[i].ActualWidth / 2, 0, 0, true);
                        }

                        // nudge left
                        for (int i = draggedGame.Index; i < dragSortedGames.Count; i++)
                        {
                            //if(gameObjects[i] != draggedGame)
                            dragSortedGames[i].MoveToAdditive(-dragSortedGames[i].ActualWidth / 2, 0, 0, true);
                        }
                    }

                    // MUST happen after initial nudge!
                    // Get all game points offset by half the width to get the center of the game
                    // Relative to window
                    gridPoints = new Point[dragSortedGames.Count];
                    UpdateGridDragPoints();

                }
                else
                {
                    // don't nudge if first or last
                    if (draggedGame.Index != 0 && draggedGame.Index != GameObjects.Count - 1)
                    {
                        // Nudge Inwards once
                        // nudge right
                        for (int i = draggedGame.Index - 1; i >= 0; i--)
                        {
                            //if (gameObjects[i] != draggedGame)
                            dragSortedGames[i].MoveToAdditive(0, dragSortedGames[i].ActualHeight / 2, 0, true);
                        }

                        // nudge left
                        for (int i = draggedGame.Index; i < dragSortedGames.Count; i++)
                        {
                            //if(gameObjects[i] != draggedGame)
                            dragSortedGames[i].MoveToAdditive(0, -dragSortedGames[i].ActualHeight / 2, 0, true);
                        }
                    }

                    // MUST happen after initial nudge!
                    // Get all game points offset by half the width to get the center of the game
                    // Relative to window
                    gridPoints = new Point[dragSortedGames.Count];
                    UpdateGridDragPoints();
                }

                // Set non draggable game positions so I don't do conversion when resetting their position
                noDragGamePositionPoints = new Point[dragSortedGames.Count];

                for (int i = 0; i < dragSortedGames.Count; i++)
                {
                    TransformGroup tg = dragSortedGames[i].RenderTransform as TransformGroup;
                    TranslateTransform trans = tg.Children[1] as TranslateTransform;

                    noDragGamePositionPoints[i] = new Point((double)trans.X, (double)trans.Y);
                    Console.WriteLine("No Drag Game point at index " + i + " is " + noDragGamePositionPoints[i]);
                }

                Console.WriteLine("Started Dragging Game");

                //lastNudgedIndex = draggedGame.Index - 1;

                isRearrangingGrid = true;
            }
        }

        static Point[] gridPoints;
        static Point[] noDragGamePositionPoints;

        static void CalculateDraggedGameCurrentGridPosition(Point mousePosRelWindow)
        {
            for (int i = 0; i < gridPoints.Length; i++)
            {
                if (Settings.Default.DockLocation <= 1)
                {
                    // Mouse is inbetween middle games
                    if (mousePosRelWindow.X > gridPoints[i].X && mousePosRelWindow.X < gridPoints[i + 1].X)
                    {
                        Console.WriteLine("Currently between " + i + " and " + (i + 1));
                        NudgeIconsForRearrange(i, i + 1);
                        return;
                    }

                    if (mousePosRelWindow.X < gridPoints[i].X)
                    {
                        Console.WriteLine("Currently below 0");
                        NudgeIconsForRearrange(-1, 0);
                        return;
                    }

                    if (mousePosRelWindow.X > gridPoints[gridPoints.Length - 1].X)
                    {
                        Console.WriteLine("Currently above last index");
                        NudgeIconsForRearrange(gridPoints.Length - 1, gridPoints.Length);
                        return;
                    }
                }
                else
                {
                    // Mouse is inbetween middle games
                    if (mousePosRelWindow.Y > gridPoints[i].Y && mousePosRelWindow.Y < gridPoints[i + 1].Y)
                    {
                        Console.WriteLine("Currently between " + i + " and " + (i + 1));
                        NudgeIconsForRearrange(i, i + 1);
                        return;
                    }

                    if (mousePosRelWindow.Y < gridPoints[i].Y)
                    {
                        Console.WriteLine("Currently below 0");
                        NudgeIconsForRearrange(-1, 0);
                        return;
                    }

                    if (mousePosRelWindow.Y > gridPoints[gridPoints.Length - 1].Y)
                    {
                        Console.WriteLine("Currently above last index");
                        NudgeIconsForRearrange(gridPoints.Length - 1, gridPoints.Length);
                        return;
                    }
                }

            }
        }

        private static void UpdateGridDragPoints()
        {

            for (int i = 0; i < gridPoints.Length; i++)
            {
                if (Settings.Default.DockLocation <= 1)
                {
                    gridPoints[i] = dragSortedGames[i].TranslatePoint(new Point(dragSortedGames[i].ActualWidth / 2, 0), AppWindow);
                    //Console.WriteLine("Game point at index "+i+" is "+gridPoints[i]);
                }
                else
                {
                    gridPoints[i] = dragSortedGames[i].TranslatePoint(new Point(0, dragSortedGames[i].ActualHeight / 2), AppWindow);
                    //Console.WriteLine("Game point at index "+i+" is "+gridPoints[i]);
                }
            }
        }

        static int lastNudgedIndex = -100;

        static void NudgeIconsForRearrange(int first, int second)
        {
            // don't nudge again if already nudged
            if (lastNudgedIndex == first)
            {
                return;
            }

            Console.WriteLine("Nudge to rearrange passed");
            lastNudgedIndex = first;

            // reset first
            for (int i = 0; i < dragSortedGames.Count; i++)
            {
                Console.WriteLine("Resetting Nudge");
                //dragSortedGames[i].MoveToRelative(AppWindow.TranslatePoint(gridPoints[i], dragSortedGames[i]).X - dragSortedGames[i].Width / 2, 0, 0);
                dragSortedGames[i].MoveTo(noDragGamePositionPoints[i].X, noDragGamePositionPoints[i].Y, 0, false, true);
            }

            if (Settings.Default.DockLocation <= 1)
            {
                // move nondragged games a whole game width rather than half on each side like when dragged game is in middle
                if (draggedGame.Index == 0)
                {
                    if (first != -1)
                    {
                        // nudge left whole width
                        for (int i = first; i >= 0; i--)
                        {
                            Console.WriteLine("Nudging left");
                            dragSortedGames[i].MoveToAdditive(-dragSortedGames[i].ActualWidth, 0, 0, true);
                        }
                    }
                }
                else if (draggedGame.Index == GameObjects.Count - 1)
                {
                    // nudge right
                    for (int i = second; i < dragSortedGames.Count; i++)
                    {
                        Console.WriteLine("Nudging right");
                        dragSortedGames[i].MoveToAdditive(dragSortedGames[i].ActualWidth, 0, 0, true);
                    }
                }
                else
                {
                    // nudge left
                    for (int i = first; i >= 0; i--)
                    {
                        Console.WriteLine("Nudging left");
                        dragSortedGames[i].MoveToAdditive(-dragSortedGames[i].ActualWidth / 2, 0, 0, true);
                    }

                    // nudge right
                    for (int i = second; i < dragSortedGames.Count; i++)
                    {
                        Console.WriteLine("Nudging right");
                        dragSortedGames[i].MoveToAdditive(dragSortedGames[i].ActualWidth / 2, 0, 0, true);
                    }
                }
            }
            else
            {
                // move nondragged games a whole game width rather than half on each side like when dragged game is in middle
                if (draggedGame.Index == 0)
                {
                    if (first != -1)
                    {
                        // nudge left whole width
                        for (int i = first; i >= 0; i--)
                        {
                            Console.WriteLine("Nudging up");
                            dragSortedGames[i].MoveToAdditive(0, -dragSortedGames[i].ActualHeight, 0, true);
                        }
                    }
                }
                else if (draggedGame.Index == GameObjects.Count - 1)
                {
                    // nudge right
                    for (int i = second; i < dragSortedGames.Count; i++)
                    {
                        Console.WriteLine("Nudging down");
                        dragSortedGames[i].MoveToAdditive(0, dragSortedGames[i].ActualHeight, 0, true);
                    }
                }
                else
                {
                    // nudge left
                    for (int i = first; i >= 0; i--)
                    {
                        Console.WriteLine("Nudging up");
                        dragSortedGames[i].MoveToAdditive(0, -dragSortedGames[i].ActualHeight / 2, 0, true);
                    }

                    // nudge right
                    for (int i = second; i < dragSortedGames.Count; i++)
                    {
                        Console.WriteLine("Nudging down");
                        dragSortedGames[i].MoveToAdditive(0, dragSortedGames[i].ActualHeight / 2, 0, true);
                    }
                }
            }

        }

        private void AnimateHighlight(bool show)
        {
            double currentOpacity = GameHighlightBorder.Opacity;

            if (show)
            {
                GameHighlightBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(currentOpacity, 1, new Duration(TimeSpan.FromSeconds(0.2d))));

                if (GameHighlightBorder.Opacity == 0)
                {

                }
            }
            else //hide
            {
                GameHighlightBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(currentOpacity, 0, new Duration(TimeSpan.FromSeconds(0.2d))));

                if (GameHighlightBorder.Opacity == 0)
                {

                }
            }

        }

        private void Game_RightClick(object sender, EventArgs e)
        {
            RightClickedGame = (Game)sender;
            // Context menu item Edit
            var editMenuItem = (MenuItem)RightClickedGame.ContextMenu.Items[2];
            //editMenuItem.Header = "_Edit ? " + RightClickedGame.DockName + " ?";
            editMenuItem.Tag = RightClickedGame.DockName;

            Console.WriteLine("IconURI: "+RightClickedGame.IconURI);
        }

        static void TestTimerExt(object s)
        {
            Console.WriteLine("Timer CALL");
        }

        #endregion

        public void UpdateGameHighlightBrush()
        {
            GameHighlightBorder.Background = GetGameHighlightBrush();
        }

        private static LinearGradientBrush GetGameHighlightBrush()
        {
            LinearGradientBrush result = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 0d);
            Color defaultColor = (Color)Application.Current.Resources["Game.ShadowColor"];
            Console.WriteLine("Default Game Shadow color is: " + defaultColor);

            switch (Settings.Default.DockLocation)
            {
                case 0:
                    result = new LinearGradientBrush(Color.FromArgb(75
                   , defaultColor.R
                   , defaultColor.B
                   , defaultColor.G)
                   , Color.FromArgb(0
                   , defaultColor.R
                   , defaultColor.G
                   , defaultColor.B), 90d);
                    break;
                case 1:
                    result = new LinearGradientBrush(Color.FromArgb(0
                    , defaultColor.R
                    , defaultColor.B
                    , defaultColor.G)
                    , Color.FromArgb(75
                    , defaultColor.R
                    , defaultColor.G
                    , defaultColor.B), 90d);
                    break;
                case 2:
                    result = new LinearGradientBrush(Color.FromArgb(75
                      , defaultColor.R
                      , defaultColor.B
                      , defaultColor.G)
                      , Color.FromArgb(0
                      , defaultColor.R
                      , defaultColor.G
                      , defaultColor.B), 0d);
                    break;
                case 3:
                    result = new LinearGradientBrush(Color.FromArgb(0
                     , defaultColor.R
                     , defaultColor.B
                     , defaultColor.G)
                     , Color.FromArgb(75
                     , defaultColor.R
                     , defaultColor.G
                     , defaultColor.B), 0d);
                    break;
                default:
                    break;
            }

            //if (Settings.Default.LocationTop)
            //{
            //    result = new LinearGradientBrush(Color.FromArgb(75
            //           , defaultColor.R
            //           , defaultColor.B
            //           , defaultColor.G)
            //           , Color.FromArgb(0
            //           , defaultColor.R
            //           , defaultColor.G
            //           , defaultColor.B), 90d);
            //}
            //else
            //{
            //    result = new LinearGradientBrush(Color.FromArgb(0
            //          , defaultColor.R
            //          , defaultColor.B
            //          , defaultColor.G)
            //          , Color.FromArgb(75
            //          , defaultColor.R
            //          , defaultColor.G
            //          , defaultColor.B), 90d);
            //}

            return result;
        }

        private static Point GetDPIScale()
        {
            PresentationSource source = PresentationSource.FromVisual(AppWindow);

            double dpiScaleX = 1d;
            double dpiScaleY = 1d;
            if (source != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            return new Point(dpiScaleX, dpiScaleY);
        }



        public static void CenterWindowOnScreen(string sender = "Not specified", bool updateLayout = true, double testNewLeft = -1d)
        {
            // SearchGridWidth + ChatGridWidth + LoadingRectangleWidth + ChatGridMargins + GameMargins + GameIconSize
            // Need to make this dynamically calculate
            //double vWidth = searchResultsGridMaxWidth + AppWindow.ChatGrid.Width + AppWindow.LoadingRectangle.Width + 16 + 10 + Settings.Default.StartupIconSize;
            //double hHeight = searchResultRowHeight * 5 + AppWindow.ChatGrid.Height + AppWindow.LoadingRectangle.Height + 8 + 10 + Settings.Default.StartupIconSize;
            double vWidth = auxGridMaxWidth + Settings.Default.StartupIconSize + 10;
            double hHeight = auxGridMaxHeight + Settings.Default.StartupIconSize + 10;
            //Console.WriteLine("vWidth is " + vWidth);
            //Console.WriteLine("hHeight is " + hHeight);
            //Console.WriteLine("Current win height is " + AppWindow.Height);
            //Console.WriteLine("Current win actual height is " + AppWindow.ActualHeight);
            //Console.WriteLine("Current win width is " + AppWindow.Width);
            //Console.WriteLine("Current win actual width is " + AppWindow.ActualWidth);

            Console.WriteLine($"<<< Center Window On Screen src: {sender} >>>");

            if (updateLayout)
            {
                AppWindow.UpdateLayout();
            }

            // To-do find a way to stick window to edges properly by using a dynamic value - maybe use updatelayout always before setting (updatelayout is slow as I recall)
            switch (Settings.Default.DockLocation)
            {
                case 0: //top
                    if (SystemParameters.VirtualScreenTop < -99)
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Top + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Height);
                    }
                    else
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Top;
                    }
                    // Fix for negative values LEFT
                    if (SystemParameters.VirtualScreenLeft < -99)
                    {
                        AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Left + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Width);
                    }
                    else
                    {
                        AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Left;
                    }

                    AppWindow.MinWidth = Position.Monitors.ActiveScreen.WorkingArea.Width;
                    AppWindow.Width = Position.Monitors.ActiveScreen.WorkingArea.Width;
                    AppWindow.MaxWidth = Position.Monitors.ActiveScreen.WorkingArea.Width;

                    // Recalculate ScrollVisibleIconCount
                    ScrollVisibleIconCount = Position.Monitors.ActiveScreen.Bounds.Width / (Settings.Default.StartupIconSize + 3);
                    break;
                case 1: //bottom
                    //AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Bottom - Position.Orientation.GetCalculatedWindowHeight();
                    // Fix for negative values TOP
                    if (SystemParameters.VirtualScreenTop < -99)
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Bottom - Position.Orientation.GetCalculatedWindowHeight() + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Height);
                    }
                    else
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Bottom - Position.Orientation.GetCalculatedWindowHeight();
                    }
                    // Fix for negative values LEFT
                    if (SystemParameters.VirtualScreenLeft < -99)
                    {
                        AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Left + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Width);
                    }
                    else
                    {
                        AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Left;
                    }

                    AppWindow.MinWidth = Position.Monitors.ActiveScreen.WorkingArea.Width;
                    AppWindow.Width = Position.Monitors.ActiveScreen.WorkingArea.Width;
                    AppWindow.MaxWidth = Position.Monitors.ActiveScreen.WorkingArea.Width;

                    // Recalculate ScrollVisibleIconCount
                    ScrollVisibleIconCount = Position.Monitors.ActiveScreen.Bounds.Width / (Settings.Default.StartupIconSize + 3);
                    break;
                case 2: //left
                    // Fix for negative values TOP
                    if (SystemParameters.VirtualScreenTop < -99)
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Top + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Height);
                    }
                    else
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Top;
                    }

                    if (testNewLeft == -1d)
                    {
                        // Fix for negative values LEFT
                        if (SystemParameters.VirtualScreenLeft < -99)
                        {
                            AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Left + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Width);
                        }
                        else
                        {
                            AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Left;
                        }
                    }
                    else
                    {
                        AppWindow.Left = testNewLeft;
                    }
                    //AppWindow.Left = newLeft;

                    //// Using SetWindowPos pinvoke
                    //Console.WriteLine("Reported Left: "+Position.Monitors.ActiveScreen.WorkingArea.Left);
                    //Console.WriteLine("Reported Location: "+Position.Monitors.ActiveScreen.WorkingArea.Location);
                    //_ = SetWindowPosNative(AppWindow, IntPtr.Zero,
                    //    Position.Monitors.ActiveScreen.WorkingArea.Left,
                    //    Position.Monitors.ActiveScreen.WorkingArea.Top,
                    //    0,
                    //    0,
                    //    SWP_NOSIZE | SWP_NOZORDER);

                    AppWindow.MinHeight = Position.Monitors.ActiveScreen.WorkingArea.Height;
                    AppWindow.Height = Position.Monitors.ActiveScreen.WorkingArea.Height;
                    AppWindow.MaxHeight = Position.Monitors.ActiveScreen.WorkingArea.Height;

                    // Recalculate ScrollVisibleIconCount
                    ScrollVisibleIconCount = Position.Monitors.ActiveScreen.Bounds.Height / (Settings.Default.StartupIconSize + 3);
                    break;
                case 3: //right
                        // Fix for negative values TOP
                    if (SystemParameters.VirtualScreenTop < -99)
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Top + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Height);
                    }
                    else
                    {
                        AppWindow.Top = Position.Monitors.ActiveScreen.WorkingArea.Top;
                    }
                    //AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Right - Position.Orientation.GetCalculatedWindowWidth();
                    // Fix for negative values LEFT
                    if (SystemParameters.VirtualScreenLeft < -99)
                    {
                        AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Right - Position.Orientation.GetCalculatedWindowWidth() + Math.Abs(Position.Monitors.ActiveScreen.WorkingArea.Width);
                    }
                    else
                    {
                        AppWindow.Left = Position.Monitors.ActiveScreen.WorkingArea.Right - Position.Orientation.GetCalculatedWindowWidth();
                    }


                    AppWindow.MinHeight = Position.Monitors.ActiveScreen.WorkingArea.Height;
                    AppWindow.Height = Position.Monitors.ActiveScreen.WorkingArea.Height;
                    AppWindow.MaxHeight = Position.Monitors.ActiveScreen.WorkingArea.Height;

                    // Recalculate ScrollVisibleIconCount
                    ScrollVisibleIconCount = Position.Monitors.ActiveScreen.Bounds.Height / (Settings.Default.StartupIconSize + 3);
                    break;
                default:
                    break;
            }


            //Console.WriteLine("MainWindow Top & Left after CenterWindowOnScreen");
            //Console.WriteLine("Top: "+AppWindow.Top);
            //Console.WriteLine("Left: "+AppWindow.Left);
            //Console.WriteLine("Actual Height: "+AppWindow.ActualHeight);
            //Console.WriteLine("Actual Width: "+AppWindow.ActualWidth);


            // Update GameHighlight
            if (AppWindow.GameHighlightBorder != null)
            {
                AppWindow.GameHighlightBorder.Background = GetGameHighlightBrush();
            }

            ToggleScrollViewerEdgeFade();

            //Console.WriteLine("ScreenWidth is " + screenWidth);
            //Console.WriteLine("Left is " + Left);
            //Console.WriteLine("Width is " + Width);
            //Console.WriteLine("ActualWidth is " + ActualWidth);
            //Console.WriteLine("WindowWidth is " + windowWidth);
            //Console.WriteLine("WindowHeight is " + windowHeight);

        }

        #region Window Events

        static double newLeft = 0;
        public static void MoveLeftPosTest(double value)
        {
            if (value != 0)
            {
                newLeft += value;
                //AppWindow.Left = newLeft;
            }

            else
            {
                //AppWindow.Left = value;
                newLeft = 0;
            }
            Console.WriteLine("newLeft: " + newLeft);
            CenterWindowOnScreen("Manual Move Left", false, newLeft);
        }

        public static void SetDockLocation()
        {
            var dpiMultiplierX = GetDPIScale().X;
            var dpiMultiplierY = GetDPIScale().Y;

            double screenWidth = MainScreenRelativeWidth / dpiMultiplierX;
            double screenHeight = MainScreenRelativeHeight / dpiMultiplierY;

            Console.WriteLine("MainScreenRelativeWidth: " + MainScreenRelativeWidth);
            Console.WriteLine("DPI Multiplier: " + dpiMultiplierX);
            Console.WriteLine("PrimaryScreenWidth: " + SystemParameters.PrimaryScreenWidth);
            Console.WriteLine("Calculated Screen Width: " + screenWidth);

            SetDockLocationDefaultValues();

            if (Settings.Default.DockLocation <= 1)
            {
                ChangeGridOrientation(GridOrientation.Horizontal);
            }
            else
            {
                ChangeGridOrientation(GridOrientation.Vertical);
            }
        }

        enum GridOrientation { Horizontal, Vertical }
        static ColumnDefinition LeftGridOffsetColumn { get; set; }
        static ColumnDefinition RightGridOffsetColumn { get; set; }
        static RowDefinition TopGridOffsetRow { get; set; }
        static RowDefinition BottomGridOffsetRow { get; set; }

        private static void ChangeGridOrientation(GridOrientation gridOrientation)
        {
            Console.WriteLine("Changing grid orientation");
            Console.WriteLine("MainParent name " + AppWindow.MainParent.Name);

            if (gridOrientation == GridOrientation.Horizontal)
            {
                // Main grid from rows to columns
                AppWindow.mainGrid.ColumnDefinitions.Clear();
                AppWindow.mainGrid.RowDefinitions.Clear();

                for (int i = 0; i < GameObjects.Count; i++)
                {
                    // Create the grid row
                    ColumnDefinition gridColumn = new ColumnDefinition
                    {
                        Width = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto)
                    };

                    AppWindow.mainGrid.ColumnDefinitions.Add(gridColumn);
                }

                // Main parent from columns to rows
                AppWindow.MainParent.RowDefinitions.Clear();
                AppWindow.MainParent.ColumnDefinitions.Clear();

                var leftRow = new RowDefinition()
                {
                    Name = "AuxPositionWhenDockLocationBottom",
                    Height = new GridLength(1, GridUnitType.Auto)
                };

                var centerRow = new RowDefinition()
                {
                    Name = "DockConstantPosition",
                    Height = new GridLength(1, GridUnitType.Auto)
                };

                var rightRow = new RowDefinition()
                {
                    Name = "AuxPositionWhenDockLocationTop",
                    Height = new GridLength(1, GridUnitType.Auto)
                };

                AppWindow.MainParent.RowDefinitions.Add(leftRow);
                AppWindow.MainParent.RowDefinitions.Add(centerRow);
                AppWindow.MainParent.RowDefinitions.Add(rightRow);

                Grid.SetRow(AppWindow.GridScrollViewer, 1);

                // Grid scroll offset parent from columns to rows
                AppWindow.GridScrollOffsetParent.ColumnDefinitions.Clear();
                AppWindow.GridScrollOffsetParent.RowDefinitions.Clear();

                var cDefLeft = new ColumnDefinition()
                {
                    Name = "GridScrollOffsetParentLeftOffset"
                };

                LeftGridOffsetColumn = cDefLeft;

                var rDefMid = new ColumnDefinition()
                {
                    Name = "GridScrollOffsetParentGridRow",
                    Width = new GridLength(1, GridUnitType.Star)
                };

                var cDefRight = new ColumnDefinition()
                {
                    Name = "GridScrollOffsetParentRightOffset"
                };

                RightGridOffsetColumn = cDefRight;

                BindScrollViewerOffsets();

                //ToggleScrollViewerOffsets();

                //if (IsDockPerfectlyFittingScreen())
                //{
                //    cDefLeft.Width = new GridLength(0, GridUnitType.Pixel);
                //    cDefRight.Width = new GridLength(0, GridUnitType.Pixel);
                //}
                //else
                //{
                //    cDefLeft.SetBinding(ColumnDefinition.WidthProperty, gridScrollOffsetBinding);
                //    cDefRight.SetBinding(ColumnDefinition.WidthProperty, gridScrollOffsetBinding);
                //}

                AppWindow.GridScrollOffsetParent.ColumnDefinitions.Add(cDefLeft);
                AppWindow.GridScrollOffsetParent.ColumnDefinitions.Add(rDefMid);
                AppWindow.GridScrollOffsetParent.ColumnDefinitions.Add(cDefRight);

                Grid.SetColumn(AppWindow.mainGrid, 1);

                // Set maingrid horizontal alignment
                AppWindow.mainGrid.HorizontalAlignment = HorizontalAlignment.Center;

                // Border stays at top for horizontal dock
                AppWindow.DockBackgroundBorder.VerticalAlignment = VerticalAlignment.Top;
                AppWindow.DockBackgroundBorder.HorizontalAlignment = HorizontalAlignment.Center;

                // Auxilary grid - holds everything but the game dock icons
                AppWindow.AuxGrid.RowDefinitions.Clear();
                AppWindow.AuxGrid.ColumnDefinitions.Clear();

                for (int i = 0; i < 3; i++)
                {
                    var rDef = new RowDefinition()
                    {
                        Name = "AuxGridColumn" + i,
                        Height = new GridLength(1, GridUnitType.Auto)
                    };

                    AppWindow.AuxGrid.RowDefinitions.Add(rDef);
                }

                // Add the column which will limit auxgrid width - probably unnecessary
                var auxGridLimitWidthColumn = new ColumnDefinition()
                {
                    Width = new GridLength(searchResultsGridMaxWidth, GridUnitType.Pixel)
                };

                AppWindow.AuxGrid.ColumnDefinitions.Add(auxGridLimitWidthColumn);

                // Align AuxGrid
                AppWindow.AuxGrid.VerticalAlignment = VerticalAlignment.Top;
                AppWindow.AuxGrid.HorizontalAlignment = HorizontalAlignment.Center;

                // Chat grid
                // Rows to columns - 2; Defaults: 70 width, 25 height
                AppWindow.ChatGrid.ColumnDefinitions.Clear();
                AppWindow.ChatGrid.RowDefinitions.Clear();

                for (int i = 0; i < 2; i++)
                {
                    var c = new ColumnDefinition()
                    {
                        Name = "ChatGridRow" + i,
                        Width = new GridLength(35, GridUnitType.Star)
                    };

                    AppWindow.ChatGrid.ColumnDefinitions.Add(c);
                }

                // Place Aux grid items depending on top or bottom

                if (Settings.Default.DockLocation == 0) // top
                {
                    // Parent
                    Grid.SetRow(AppWindow.AuxGrid, 2);
                    //----------->
                    Grid.SetRow(AppWindow.LoadingRectangle, 0);
                    Grid.SetRow(AppWindow.ChatGrid, 1);
                    Grid.SetRow(AppWindow.SearchBarParent, 1);
                    Grid.SetRow(AppWindow.SearchResultsGrid, 2);

                    // Lock the height of the search bar column for positioning purposes
                    AppWindow.AuxGrid.RowDefinitions[2].Height = new GridLength(searchResultRowHeight * 5, GridUnitType.Pixel);

                    // Align Search Results grid
                    AppWindow.SearchResultsGrid.VerticalAlignment = VerticalAlignment.Top;
                }

                if (Settings.Default.DockLocation == 1) // bottom
                {
                    // Parent
                    Grid.SetRow(AppWindow.AuxGrid, 0);
                    //----------->
                    Grid.SetRow(AppWindow.LoadingRectangle, 2);
                    Grid.SetRow(AppWindow.ChatGrid, 1);
                    Grid.SetRow(AppWindow.SearchBarParent, 1);
                    Grid.SetRow(AppWindow.SearchResultsGrid, 0);

                    // Lock the height of the search bar column for positioning purposes
                    AppWindow.AuxGrid.RowDefinitions[0].Height = new GridLength(searchResultRowHeight * 5, GridUnitType.Pixel);

                    // Align Search Results grid
                    AppWindow.SearchResultsGrid.VerticalAlignment = VerticalAlignment.Bottom;
                }

                // Change PanningMode
                AppWindow.GridScrollViewer.PanningMode = PanningMode.HorizontalOnly;

                // Align search bar and results - common
                AppWindow.SearchBarParent.VerticalAlignment = VerticalAlignment.Center;
                AppWindow.SearchBarParent.HorizontalAlignment = HorizontalAlignment.Center;

                AppWindow.SearchResultsGrid.HorizontalAlignment = HorizontalAlignment.Center;

                // Set search to the newly created limited Width column
                for (int i = 0; i < AppWindow.AuxGrid.Children.Count; i++)
                {
                    Grid.SetColumn(AppWindow.AuxGrid.Children[i], 0);
                }
                //Grid.SetColumn(AppWindow.SearchBarParent, 0);
                //Grid.SetColumn(AppWindow.SearchResultsGrid, 0);

                // ScrollViewer Opacity Mask (top and bottom fade)

            }

            // VERTICAL -----------------------------------------------------
            if (gridOrientation == GridOrientation.Vertical)
            {
                // Main grid from columns to rows
                AppWindow.mainGrid.ColumnDefinitions.Clear();
                AppWindow.mainGrid.RowDefinitions.Clear();

                for (int i = 0; i < GameObjects.Count; i++)
                {
                    // Create the grid row
                    RowDefinition gridRow = new RowDefinition
                    {
                        Height = new GridLength(Settings.Default.StartupIconSize, GridUnitType.Auto)
                    };

                    AppWindow.mainGrid.RowDefinitions.Add(gridRow);
                }

                // Main parent from rows to columns
                AppWindow.MainParent.RowDefinitions.Clear();
                AppWindow.MainParent.ColumnDefinitions.Clear();

                var leftColumn = new ColumnDefinition()
                {
                    Name = "AuxPositionWhenDockLocationRight",
                    Width = new GridLength(1, GridUnitType.Auto)
                };

                var centerColumn = new ColumnDefinition()
                {
                    Name = "DockConstantPosition",
                    Width = new GridLength(1, GridUnitType.Auto)
                };

                var rightColumn = new ColumnDefinition()
                {
                    Name = "AuxPositionWhenDockLocationRight",
                    Width = new GridLength(1, GridUnitType.Auto)
                };

                AppWindow.MainParent.ColumnDefinitions.Add(leftColumn);
                AppWindow.MainParent.ColumnDefinitions.Add(centerColumn);
                AppWindow.MainParent.ColumnDefinitions.Add(rightColumn);

                Grid.SetColumn(AppWindow.GridScrollViewer, 1);

                // Grid scroll offset parent from columns to rows
                AppWindow.GridScrollOffsetParent.ColumnDefinitions.Clear();
                AppWindow.GridScrollOffsetParent.RowDefinitions.Clear();

                var rDefTop = new RowDefinition()
                {
                    Name = "GridScrollOffsetParentTopOffset"
                };

                TopGridOffsetRow = rDefTop;

                var rDefMid = new RowDefinition()
                {
                    Name = "GridScrollOffsetParentGridRow",
                    Height = new GridLength(1, GridUnitType.Star)
                };

                var rDefBottom = new RowDefinition()
                {
                    Name = "GridScrollOffsetParentBottomOffset"
                };

                BottomGridOffsetRow = rDefBottom;

                BindScrollViewerOffsets();

                //ToggleScrollViewerOffsets();

                //if (IsDockPerfectlyFittingScreen())
                //{
                //    rDefTop.Height = new GridLength(0, GridUnitType.Pixel);
                //    rDefBottom.Height = new GridLength(0, GridUnitType.Pixel);
                //}
                //else
                //{
                //    rDefTop.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
                //    rDefBottom.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
                //}

                AppWindow.GridScrollOffsetParent.RowDefinitions.Add(rDefTop);
                AppWindow.GridScrollOffsetParent.RowDefinitions.Add(rDefMid);
                AppWindow.GridScrollOffsetParent.RowDefinitions.Add(rDefBottom);

                Grid.SetRow(AppWindow.mainGrid, 1);

                // Set maingrid to center vertically
                AppWindow.mainGrid.VerticalAlignment = VerticalAlignment.Center;

                // Border centers vertically for left/right dock
                AppWindow.DockBackgroundBorder.VerticalAlignment = VerticalAlignment.Center;
                AppWindow.DockBackgroundBorder.HorizontalAlignment = HorizontalAlignment.Left;

                // Auxilary grid - holds everything but the game dock icons
                AppWindow.AuxGrid.RowDefinitions.Clear();
                AppWindow.AuxGrid.ColumnDefinitions.Clear();

                for (int i = 0; i < 3; i++)
                {
                    var cDef = new ColumnDefinition()
                    {
                        Name = "AuxGridColumn" + i,
                        Width = new GridLength(1, GridUnitType.Auto)
                    };

                    AppWindow.AuxGrid.ColumnDefinitions.Add(cDef);
                }

                var searchRow = new RowDefinition()
                {
                    // 10 is offset for space between the search bar and results
                    Height = new GridLength(AppWindow.SearchTextBox.ActualHeight + searchResultRowHeight * 5 + 10, GridUnitType.Pixel)
                };

                AppWindow.AuxGrid.RowDefinitions.Add(searchRow);

                // Align AuxGrid
                AppWindow.AuxGrid.VerticalAlignment = VerticalAlignment.Center;
                AppWindow.AuxGrid.HorizontalAlignment = HorizontalAlignment.Left;

                // Chat grid
                // Columns to rows - 2; Defaults: 70 width, 25 height
                AppWindow.ChatGrid.ColumnDefinitions.Clear();
                AppWindow.ChatGrid.RowDefinitions.Clear();

                for (int i = 0; i < 2; i++)
                {
                    var row = new RowDefinition()
                    {
                        Name = "ChatGridRow" + i,
                        Height = new GridLength(35, GridUnitType.Star)
                    };

                    AppWindow.ChatGrid.RowDefinitions.Add(row);
                }

                // Place Aux grid items depending on left or right

                if (Settings.Default.DockLocation == 2) // left
                {
                    // Parent
                    Grid.SetColumn(AppWindow.AuxGrid, 2);
                    //----------->
                    Grid.SetColumn(AppWindow.LoadingRectangle, 0);
                    Grid.SetColumn(AppWindow.ChatGrid, 1);
                    Grid.SetColumn(AppWindow.SearchBarParent, 2);
                    Grid.SetColumn(AppWindow.SearchResultsGrid, 2);

                    // Lock the width of the search bar column for positioning purposes
                    AppWindow.AuxGrid.ColumnDefinitions[2].Width = new GridLength(searchResultsGridMaxWidth, GridUnitType.Pixel);
                }

                if (Settings.Default.DockLocation == 3) // right
                {
                    // Parent
                    Grid.SetColumn(AppWindow.AuxGrid, 0);
                    //----------->
                    Grid.SetColumn(AppWindow.LoadingRectangle, 2);
                    Grid.SetColumn(AppWindow.ChatGrid, 1);
                    Grid.SetColumn(AppWindow.SearchBarParent, 0);
                    Grid.SetColumn(AppWindow.SearchResultsGrid, 0);

                    // Lock the width of the search bar column for positioning purposes
                    AppWindow.AuxGrid.ColumnDefinitions[0].Width = new GridLength(searchResultsGridMaxWidth, GridUnitType.Pixel);
                }

                AppWindow.GridScrollViewer.PanningMode = PanningMode.VerticalOnly;

                //// Align search bar and results
                //AppWindow.SearchBarParent.VerticalAlignment = VerticalAlignment.Top;
                //AppWindow.SearchResultsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                //AppWindow.SearchBarParent.HorizontalAlignment = HorizontalAlignment.Center;
                //AppWindow.SearchResultsGrid.HorizontalAlignment = HorizontalAlignment.Center;
                // Replaces above code
                AdjustVerticalSearchPosition();

                // Set row to the newly created limited Height row, so search bar and results are in the same column
                Grid.SetRow(AppWindow.SearchBarParent, 0);
                Grid.SetRow(AppWindow.SearchResultsGrid, 0);
            }

            ToggleScrollViewerEdgeFade();
            AppWindow.SetChatBarPositionSizeMargin();
            AppWindow.SetChatBarVisibility(Settings.Default.ShowChatBar);
            RedoGameObjectOrientation(gridOrientation);
        }

        public static void AdjustVerticalSearchPosition()
        {
            if (Settings.Default.SearchSteam)
            {
                // Align search bar and results
                AppWindow.SearchBarParent.VerticalAlignment = VerticalAlignment.Top;
                AppWindow.SearchResultsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                AppWindow.SearchBarParent.HorizontalAlignment = HorizontalAlignment.Center;
                AppWindow.SearchResultsGrid.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {

                // Align search bar and results
                AppWindow.SearchBarParent.VerticalAlignment = VerticalAlignment.Center;
                AppWindow.SearchResultsGrid.VerticalAlignment = VerticalAlignment.Bottom;

                if (Settings.Default.DockLocation == 2) // left
                {
                    AppWindow.SearchBarParent.HorizontalAlignment = HorizontalAlignment.Left;
                    AppWindow.SearchResultsGrid.HorizontalAlignment = HorizontalAlignment.Center;
                }

                if (Settings.Default.DockLocation == 3) // right
                {
                    AppWindow.SearchBarParent.HorizontalAlignment = HorizontalAlignment.Right;
                    AppWindow.SearchResultsGrid.HorizontalAlignment = HorizontalAlignment.Center;
                }

            }
        }

        static int lastScrollableDistance;
        public static void ToggleScrollViewerOffsets(int scrollableDistance = -1)
        {
            if (IsDockHorizontal)
            {
                if (IsDockPerfectlyFittingScreen)
                {
                    LeftGridOffsetColumn.Width = new GridLength(0, GridUnitType.Pixel);
                    LeftGridOffsetColumn.MinWidth = 0;
                    LeftGridOffsetColumn.MaxWidth = 0;
                    RightGridOffsetColumn.Width = new GridLength(0, GridUnitType.Pixel);
                    RightGridOffsetColumn.MinWidth = 0;
                    RightGridOffsetColumn.MaxWidth = 0;
                }
                else
                {
                    // prevent being called many times by ScrollViewer_ScrollChanged event
                    if (lastScrollableDistance != scrollableDistance || scrollableDistance == -1)
                    {
                        var newWidth = new GridLength(AppWindow.GridScrollViewer.ScrollableWidth * 0.1d + GradientOffsetPixels, GridUnitType.Pixel);

                        LeftGridOffsetColumn.Width = newWidth;
                        LeftGridOffsetColumn.MinWidth = newWidth.Value;
                        LeftGridOffsetColumn.MaxWidth = newWidth.Value;
                        RightGridOffsetColumn.Width = newWidth;
                        RightGridOffsetColumn.MinWidth = newWidth.Value;
                        RightGridOffsetColumn.MaxWidth = newWidth.Value;
                        lastScrollableDistance = scrollableDistance;
                    }

                    //// Change height to binding 10% of window actual width using converter
                    //Binding gridScrollOffsetBinding = new Binding("ActualWidth")
                    //{
                    //    Source = AppWindow.MainParent,
                    //    Converter = PercentageOfElementConverter,
                    //    ConverterParameter = 0.1
                    //};

                    //LeftGridOffsetColumn.SetBinding(ColumnDefinition.WidthProperty, gridScrollOffsetBinding);
                    //RightGridOffsetColumn.SetBinding(ColumnDefinition.WidthProperty, gridScrollOffsetBinding);
                }
            }
            else
            {
                if (IsDockPerfectlyFittingScreen)
                {
                    #region Using Code
                    //TopGridOffsetRow.Height = new GridLength(0, GridUnitType.Pixel);
                    //TopGridOffsetRow.MinHeight = 0;
                    //TopGridOffsetRow.MaxHeight= 0;
                    //BottomGridOffsetRow.Height = new GridLength(0, GridUnitType.Pixel);
                    //BottomGridOffsetRow.MinHeight= 0;
                    //BottomGridOffsetRow.MaxHeight= 0;
                    #endregion

                    Binding gridScrollOffsetBinding = new Binding("ScrollableHeight")
                    {
                        Source = AppWindow.GridScrollViewer,
                        Converter = ScrollViewerOffsetsConverter,
                        ConverterParameter = 0d
                    };

                    TopGridOffsetRow.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
                    BottomGridOffsetRow.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);

                }
                else
                {
                    #region Using Code
                    //// prevent being called many times by ScrollViewer_ScrollChanged event
                    //// topgridoffsetrow doesn't seem to resize properly on updategameiconsizes
                    //if (lastScrollableDistance != scrollableDistance || scrollableDistance == -1)
                    //{
                    //    var newHeight = new GridLength(AppWindow.GridScrollViewer.ScrollableHeight * lowerScrollLimit + gradientOffsetPixels, GridUnitType.Pixel);

                    //    TopGridOffsetRow.Height = newHeight;
                    //    TopGridOffsetRow.MinHeight = newHeight.Value;
                    //    TopGridOffsetRow.MaxHeight = newHeight.Value;
                    //    BottomGridOffsetRow.Height = newHeight;
                    //    BottomGridOffsetRow.MinHeight = newHeight.Value;
                    //    BottomGridOffsetRow.MaxHeight = newHeight.Value;
                    //    lastScrollableDistance = scrollableDistance;
                    //    Console.WriteLine("TOGGLE OFFSETS: Setting offsets");
                    //    Console.WriteLine("LastScrollableDistance " + lastScrollableDistance);
                    //    Console.WriteLine("scrollableDistance " + scrollableDistance);
                    //    Console.WriteLine("Offset Row Height "+TopGridOffsetRow.Height);
                    //    Console.WriteLine("Offset Row Actual Height "+TopGridOffsetRow.ActualHeight);
                    //}

                    #endregion

                    #region Using Binding
                    // Change height to binding 10% of window actual height using converter
                    //AppWindow.GridScrollViewer.ScrollableHeight * lowerScrollLimit + gradientOffsetPixels
                    Binding gridScrollOffsetBinding = new Binding("ScrollableHeight")
                    {
                        Source = AppWindow.GridScrollViewer,
                        Converter = ScrollViewerOffsetsConverter,
                        ConverterParameter = 0.1d
                    };

                    TopGridOffsetRow.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
                    BottomGridOffsetRow.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
                    #endregion
                }

                Console.WriteLine("MAIN GRID Actual Height " + AppWindow.mainGrid.ActualHeight);
                Console.WriteLine("Offset Row Height " + TopGridOffsetRow.Height);
                Console.WriteLine("Offset Row Actual Height " + TopGridOffsetRow.ActualHeight);
                Console.WriteLine("GradientOffsetPixels " + GradientOffsetPixels);
            }

        }

        private static void BindScrollViewerOffsets()
        {
            double autoSizePadding = 25;

            // When auto-sizing, use fixed small padding instead of game-width binding
            if (Settings.Default.BackgroundAutoSize)
            {
                if (IsDockHorizontal)
                {
                    // Horizontal dock: add padding on left/right sides only
                    if (LeftGridOffsetColumn != null)
                    {
                        BindingOperations.ClearBinding(LeftGridOffsetColumn, ColumnDefinition.WidthProperty);
                        LeftGridOffsetColumn.Width = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        LeftGridOffsetColumn.MinWidth = autoSizePadding;
                        LeftGridOffsetColumn.MaxWidth = autoSizePadding;
                    }
                    if (RightGridOffsetColumn != null)
                    {
                        BindingOperations.ClearBinding(RightGridOffsetColumn, ColumnDefinition.WidthProperty);
                        RightGridOffsetColumn.Width = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        RightGridOffsetColumn.MinWidth = autoSizePadding;
                        RightGridOffsetColumn.MaxWidth = autoSizePadding;
                    }
                }
                else
                {
                    // Vertical dock: add padding on top/bottom sides only
                    if (TopGridOffsetRow != null)
                    {
                        BindingOperations.ClearBinding(TopGridOffsetRow, RowDefinition.HeightProperty);
                        TopGridOffsetRow.Height = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        TopGridOffsetRow.MinHeight = autoSizePadding;
                        TopGridOffsetRow.MaxHeight = autoSizePadding;
                    }
                    if (BottomGridOffsetRow != null)
                    {
                        BindingOperations.ClearBinding(BottomGridOffsetRow, RowDefinition.HeightProperty);
                        BottomGridOffsetRow.Height = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        BottomGridOffsetRow.MinHeight = autoSizePadding;
                        BottomGridOffsetRow.MaxHeight = autoSizePadding;
                    }
                }
                return;
            }

            if (IsDockHorizontal)
            {
                //Bind offsets to 1 game width
                Binding gridScrollOffsetBinding = new Binding("ActualGameSize")
                {
                    Mode = BindingMode.OneWay,
                    Source = AppWindow
                };

                LeftGridOffsetColumn.SetBinding(ColumnDefinition.WidthProperty, gridScrollOffsetBinding);
                RightGridOffsetColumn.SetBinding(ColumnDefinition.WidthProperty, gridScrollOffsetBinding);
            }
            else
            {
                //Bind offsets to 1 game width
                Binding gridScrollOffsetBinding = new Binding("ActualGameSize")
                {
                    Mode = BindingMode.OneWay,
                    Source = AppWindow
                };

                TopGridOffsetRow.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
                BottomGridOffsetRow.SetBinding(RowDefinition.HeightProperty, gridScrollOffsetBinding);
            }
        }

        public static double GradientOffsetPixels
        {
            get
            {
                return Settings.Default.StartupIconSize / 2d;
            }
        }

        static int lastScrollviewerDimension;

        public static void ToggleScrollViewerEdgeFade(int scrollviewerDimension = -1)
        {
            if (IsDockPerfectlyFittingScreen)
            {
                //Console.WriteLine("DOCK PERFECTLY FITS");
                AppWindow.GridScrollViewer.ClearValue(OpacityMaskProperty);
                return;
            }

            //Console.WriteLine("DOCK DOESN'T FIT");

            if (Settings.Default.DockEdgeFadeEnabled)
            {
                // Calculate gradient stop offset from icon size
                double normalizedGradientOffset = 0.1d;

                if (Settings.Default.DockLocation <= 1)
                {
                    if (AppWindow.GridScrollViewer.ActualWidth != 0)
                    {
                        if (lastScrollviewerDimension != scrollviewerDimension || scrollviewerDimension == -1)
                        {
                            normalizedGradientOffset = GradientOffsetPixels / AppWindow.GridScrollViewer.ActualWidth;
                            lastScrollviewerDimension = scrollviewerDimension;
                        }
                    }
                }
                else
                {
                    if (AppWindow.GridScrollViewer.ActualHeight != 0)
                    {
                        if (lastScrollviewerDimension != scrollviewerDimension || scrollviewerDimension == -1)
                        {
                            normalizedGradientOffset = GradientOffsetPixels / AppWindow.GridScrollViewer.ActualHeight;
                            lastScrollviewerDimension = scrollviewerDimension;
                            Console.WriteLine("EDGE FADE: Setting Normalized Gradient Offset " + normalizedGradientOffset);
                            Console.WriteLine("lastScrollViewerDimension " + lastScrollviewerDimension);
                            Console.WriteLine("scrollViewerDimension " + scrollviewerDimension);
                        }
                    }
                }

                //Console.WriteLine("Normalized Gradient Offset for Edge Fade " + normalizedGradientOffset);

                // Gradient stops
                var gStops = new GradientStopCollection()
                {
                    new GradientStop(Colors.Transparent, 0d),
                    new GradientStop(Colors.White, normalizedGradientOffset),
                    new GradientStop(Colors.White, 1 - normalizedGradientOffset),
                    new GradientStop(Colors.Transparent, 1d)
                };

                // Set the opacity mask
                var mask = new LinearGradientBrush(gStops);
                AppWindow.GridScrollViewer.OpacityMask = mask;

                //Horizontal
                if (Settings.Default.DockLocation <= 1)
                {
                    mask.StartPoint = new Point(0, 0.5);
                    mask.EndPoint = new Point(1, 0.5);
                }
                else // Vertical
                {
                    mask.StartPoint = new Point(0.5, 0);
                    mask.EndPoint = new Point(0.5, 1);
                }

            }
            else
            {
                AppWindow.GridScrollViewer.ClearValue(OpacityMaskProperty);
            }
        }
        private static void RedoGameObjectOrientation(GridOrientation orientation)
        {
            if (orientation == GridOrientation.Horizontal)
            {
                // from rows to columns
                for (int i = 0; i < GameObjects.Count; i++)
                {
                    Grid.SetColumn(GameObjects[i], i);
                    GameObjects[i].OwnedColumnDefinition = AppWindow.mainGrid.ColumnDefinitions[i];
                }
            }

            if (orientation == GridOrientation.Vertical)
            {
                // from columns to rows
                for (int i = 0; i < GameObjects.Count; i++)
                {
                    Grid.SetRow(GameObjects[i], i);
                    GameObjects[i].OwnedRowDefinition = AppWindow.mainGrid.RowDefinitions[i];
                }
            }

            for (int i = 0; i < GameObjects.Count; i++)
            {
                GameObjects[i].Margin = DefaultGameMargins;
                //UpdateGameTooltip(gameObjects[i]);
            }
        }


        private void MainWindow_GotFocus(object sender, RoutedEventArgs e) //to be used with Controls
        {
            //Console.WriteLine("MW GOT FOCUS");
        }

        private void MainWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine("MW LOST FOCUS");
            //GameHighlightBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.2d))));

        }

        public void MainWindow_Activated(object sender, EventArgs e)
        {
            Console.WriteLine("MainWindow was activated");
            MainWindowActive = true;

        }

        private static DispatcherTimer autoScanForGamesTimer = LoadAutoScanForGamesTimer();
        static DispatcherTimer LoadAutoScanForGamesTimer()
        {
            var res = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMinutes(5d) // 5 min default
            };

            res.Tick += AutoScanForGamesTimer_Tick;
            return res;
        }

        private static void AutoScanForGamesTimer_Tick(object sender, EventArgs e)
        {
            RescanAsync();
        }

        public static void ToggleAutoScanForGames()
        {
            if (Settings.Default.AutoScanForGames)
            {
                autoScanForGamesTimer.Start();
            }
            else
            {
                autoScanForGamesTimer.Stop();
            }
            //if (Settings.Default.AutoScanForGames)
            //    RescanAsync();
        }


        public static void ForceDeactivate()
        {
            if (AppWindow == null) return;

            AppWindow.Dispatcher.Invoke(() =>
            {
                // Replicate logic from MainWindow_Deactivated

                // Hide GameHighlight
                AppWindow.AnimateHighlight(false);

                // Hide search
                AppWindow.ShrinkSearchBox();

                // Hide tooltip
                GameTooltip.IsOpen = false;

                // Reset HighlightedGame
                HighlightedGame = null;

                // Clear Search
                AppWindow.SearchTextBox.Text = string.Empty;
            });
        }

        public static void UpdateBackgroundSize()
        {
            if (AppWindow == null) return;

            AppWindow.Dispatcher.Invoke(() =>
            {
                if (Settings.Default.BackgroundAutoSize)
                {
                    AppWindow.DockBackgroundBorder.Width = double.NaN;
                    AppWindow.DockBackgroundBorder.Height = double.NaN;

                    // Use small fixed offset so auto-sized background has nice padding around icons
                    double autoSizePadding = 25;
                    if (LeftGridOffsetColumn != null)
                    {
                        BindingOperations.ClearBinding(LeftGridOffsetColumn, ColumnDefinition.WidthProperty);
                        LeftGridOffsetColumn.Width = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        LeftGridOffsetColumn.MinWidth = autoSizePadding;
                        LeftGridOffsetColumn.MaxWidth = autoSizePadding;
                    }
                    if (RightGridOffsetColumn != null)
                    {
                        BindingOperations.ClearBinding(RightGridOffsetColumn, ColumnDefinition.WidthProperty);
                        RightGridOffsetColumn.Width = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        RightGridOffsetColumn.MinWidth = autoSizePadding;
                        RightGridOffsetColumn.MaxWidth = autoSizePadding;
                    }
                    if (TopGridOffsetRow != null)
                    {
                        BindingOperations.ClearBinding(TopGridOffsetRow, RowDefinition.HeightProperty);
                        TopGridOffsetRow.Height = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        TopGridOffsetRow.MinHeight = autoSizePadding;
                        TopGridOffsetRow.MaxHeight = autoSizePadding;
                    }
                    if (BottomGridOffsetRow != null)
                    {
                        BindingOperations.ClearBinding(BottomGridOffsetRow, RowDefinition.HeightProperty);
                        BottomGridOffsetRow.Height = new GridLength(autoSizePadding, GridUnitType.Pixel);
                        BottomGridOffsetRow.MinHeight = autoSizePadding;
                        BottomGridOffsetRow.MaxHeight = autoSizePadding;
                    }
                }
                else
                {
                    if (Settings.Default.BackgroundEdgeToEdge)
                    {
                        AppWindow.DockBackgroundBorder.Width = SystemParameters.PrimaryScreenWidth;
                    }
                    else
                    {
                        AppWindow.DockBackgroundBorder.Width = Settings.Default.BackgroundWidth;
                    }

                    AppWindow.DockBackgroundBorder.Height = Settings.Default.BackgroundHeight;

                    // Restore offset column bindings
                    BindScrollViewerOffsets();
                }
            });
        }

        public static void UpdateBackgroundVisuals()
        {
            if (AppWindow == null) return;

            AppWindow.Dispatcher.Invoke(() =>
            {
                // Color - using logic from ScrollViewerBackgroundColorConverter
                Color baseColor = Settings.Default.DarkGameOutline
                    ? Color.FromArgb((byte)(Settings.Default.BackgroundOpacity * 255), 0, 0, 0)
                    : Color.FromArgb((byte)(Settings.Default.BackgroundOpacity * 255), 255, 255, 255);

                AppWindow.DockBackgroundBorder.Background = new SolidColorBrush(baseColor);

                // Corner Radius
                AppWindow.DockBackgroundBorder.CornerRadius = new CornerRadius(Settings.Default.BackgroundCornerRadius);
            });
        }

        public static void UpdateIconMargins()
        {
            if (AppWindow == null) return;

            AppWindow.Dispatcher.Invoke(() =>
            {
                SetDockLocationDefaultValues();

                for (int i = 0; i < GameObjects.Count; i++)
                {
                    Game game = GameObjects[i];
                    
                    if (Settings.Default.DockLocation <= 1) // top, bottom (horizontal)
                    {
                        // First icon: no left margin
                        // Last icon: no right margin
                        // Middle icons: full margins
                        double leftMargin = (i == 0) ? 0 : Settings.Default.IconSpacing;
                        double rightMargin = (i == GameObjects.Count - 1) ? 0 : Settings.Default.IconSpacing;
                        game.Margin = new Thickness(leftMargin, 5, rightMargin, 5);
                    }
                    else // left, right (vertical)
                    {
                        // First icon: no top margin
                        // Last icon: no bottom margin
                        // Middle icons: full margins
                        double topMargin = (i == 0) ? 0 : Settings.Default.IconSpacing;
                        double bottomMargin = (i == GameObjects.Count - 1) ? 0 : Settings.Default.IconSpacing;
                        game.Margin = new Thickness(5, topMargin, 5, bottomMargin);
                    }
                }
            });
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            Console.WriteLine("MainWindow was deactivated");
            MainWindowActive = false;

            // Fix Search Textbox
            SearchTextBox.Text = string.Empty;

            // Hide GameHighlight
            AnimateHighlight(false);

            // Hide search
            ShrinkSearchBox();

            // Hide tooltip
            GameTooltip.IsOpen = false;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            Console.WriteLine("WINDOW STATE" + WindowState.ToString());
            
            // Prevent minimize-all from hiding the dock
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
        }
        #endregion

        public static void RescanAsync()
        {
            Scanner.Rescan.UpdatePrograms();
        }

        private void GridScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Dock.Scrolling.ScrollViewer_PreviewMouseWheel(sender, e);
        }

        public static bool IsDockPerfectlyFittingScreen => GetIsDockPerfectlyFittingScreen();

        public static bool GetIsDockPerfectlyFittingScreen()
        {
            if (AppWindow == null) return true;

            bool fits = AppWindow.GridScrollViewer.ScrollableWidth == 0 && AppWindow.GridScrollViewer.ScrollableHeight == 0;
            DockScrollingDisallowed = fits;
            return fits;
        }

        private void Mediator_Loaded(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine("Scrollable width: " + Mediator.ScrollViewer.ScrollableWidth);
            //EnableSmoothScrolling();
        }

        private void GridScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            Dock.Scrolling.EnableSmoothScrolling();
        }

        // Add game on dropped
        private void DragBorder_Drop(object sender, DragEventArgs e)
        {
            CustomUIElements.DragAdornerInitializer.ClearAdorner(true);

            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];

                if (!string.IsNullOrEmpty(paths[0]))
                    AddGameManually(true, paths[0]);
                // handle the files here!
            }
        }

        public static bool checkDragExtensionOnce;

        private void DragBorder_DragOver(object sender, DragEventArgs e)
        {
            Border sv = sender as Border;

            if (!checkDragExtensionOnce)
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];

                    if (!string.IsNullOrEmpty(paths[0]))
                    {
                        if (Path.GetExtension(paths[0]) == ".exe" ||
                            Path.GetExtension(paths[0]) == ".url" ||
                            Path.GetExtension(paths[0]) == ".lnk")
                        {
                            // Accepted
                            //e.Effects = DragDropEffects.Link;
                            DragBorder.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                            Console.WriteLine("Dragged format is accepted");
                        }
                        else
                        {
                            //e.Effects = DragDropEffects.None;
                            DragBorder.Background = new SolidColorBrush(Color.FromArgb(55, 230, 22, 22));
                            Console.WriteLine("Dragged format is NOT accepted");
                        }
                    }
                    // handle the files here!
                }
                checkDragExtensionOnce = true;
            }

            CustomUIElements.DragAdornerInitializer.RunAdorner(sv, e);
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(RightClickedGame.UninstallPath) == false)
                {
                    var result = OpenWindow.Notification($"You are about to uninstall {RightClickedGame.DisplayName}. Continue?", $"Uninstall {RightClickedGame.DisplayName}?", OpenWindow.NotificationWindowType.YesNo);

                    if (result == OpenWindow.NotificationResult.Yes)
                    {
                        string[] parsed = ParseUninstallString(RightClickedGame.UninstallPath);

                        if (File.Exists(parsed[0]))
                        {
                            Process unins = new Process();
                            unins.StartInfo.FileName = parsed[0];
                            unins.StartInfo.Arguments = parsed[1];
                            unins.EnableRaisingEvents = true;
                            unins.Exited += Unins_Exited;
                            unins.Start();


                            unins.WaitForExit();
                            //RescanAsync();
                            unins.Dispose();

                            System.Threading.Thread.Sleep(500);

                            if (!Directory.Exists(Path.GetDirectoryName(parsed[0])) || !File.Exists(parsed[0]))
                            {
                                Console.WriteLine("Removing game after uninstall");
                                RightClickedGame.Remove();
                            }
                            else
                            {
                                Console.WriteLine("Game was not uninstalled");
                            }
                        }
                        else
                        {
                            OpenWindow.Notification("Couldn't find uninstall path, try uninstalling from Add/Remove Programs", "Invalid Path");
                        }
                    }
                }
                else if (RightClickedGame.Launcher == BelongsToLauncher.UWP)
                {
                    ProcessStartInfo uninsUWPinfo = new ProcessStartInfo();

                    uninsUWPinfo.UseShellExecute = true;
                    uninsUWPinfo.CreateNoWindow = false;
                    uninsUWPinfo.Arguments = $"get-appxpackage *{RightClickedGame.UWPAppID}* | remove-appxpackage";
                    uninsUWPinfo.WindowStyle = ProcessWindowStyle.Hidden;
                    uninsUWPinfo.FileName = "powershell.exe";
                    var proc = Process.Start(uninsUWPinfo);

                    Console.WriteLine(uninsUWPinfo.Arguments, uninsUWPinfo.FileName);

                    RightClickedGame.Remove();
                    //proc.WaitForExit();
                    //var exitcode = proc.ExitCode;
                }
                else
                {
                    OpenWindow.Notification("Couldn't find uninstall path, try uninstalling from Add/Remove Programs", "Invalid Path");

                }
            }
            catch (Exception)
            {
                OpenWindow.Notification("Couldn't find uninstall path, try uninstalling from Add/Remove Programs", "Invalid Path");
            }
        }

        private void Unins_Exited(object sender, EventArgs e)
        {
            Console.WriteLine("Unins Process Exited");
        }

        private string[] ParseUninstallString(string unins)
        {
            string[] result = new string[2];

            System.Text.RegularExpressions.MatchCollection matches = System.Text.RegularExpressions.Regex.Matches(unins, "\"(.*?)\"");

            if (matches.Count > 0)
            {
                result[0] = matches[0].Value.Replace("\"", string.Empty);
                result[1] = unins.Replace(matches[0].Value, string.Empty);
            }
            else
            {
                result[0] = unins;
                result[1] = string.Empty;
            }

            return result;
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ExpandSearchBox();
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            //TextBox tbox = sender as TextBox;

            //if (tbox.Text.Length != 0)
            //{
            //    tbox.Text = string.Empty;
            //}

            //// Hide web search results
            //SearchResultsGrid.Children.Clear();
            //SearchResultsGrid.RowDefinitions.Clear();

            //var dt = new System.Windows.Threading.DispatcherTimer();
            //dt.Tick += RestoreChatIconVisibilityDelayed_Tick;
            //dt.Interval = TimeSpan.FromMilliseconds(200d);
            //dt.Start();

            //RearrangeGrid(gameObjects);
            //RefreshGrid();
            //CenterWindowOnScreen();
        }

        private static bool IsSearchBoxExpanded { get; set; }

        private void ExpandSearchBox()
        {
            if (IsSearchBoxExpanded == false)
            {
                Console.WriteLine("Expanding search box");

                SearchTextBox.BeginStoryboard((Storyboard)TryFindResource("SearchBoxExpand"));

                if (SearchTextBox.Text.Length != 0)
                {
                    SearchTextBox.Text = string.Empty;
                }

                SearchIconImage.Visibility = Visibility.Hidden;

                ChangeChatIconsVisibilityState(true);

                IsSearchBoxExpanded = true;
            }

        }

        private void ShrinkSearchBox()
        {
            if (IsSearchBoxExpanded == true)
            {
                SearchTextBox.BeginStoryboard((Storyboard)TryFindResource("SearchBoxShrink"));

                if (SearchTextBox.Text.Length != 0)
                {
                    SearchTextBox.Text = string.Empty;
                }

                // Hide web search results
                SearchResultsGrid.Children.Clear();
                SearchResultsGrid.RowDefinitions.Clear();

                var dt = new System.Windows.Threading.DispatcherTimer();
                dt.Tick += RestoreChatIconVisibilityDelayed_Tick;
                dt.Interval = TimeSpan.FromMilliseconds(200d);
                dt.Start();

                RearrangeGrid(GameObjects, true);
                RefreshGrid();
                CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);

                IsSearchBoxExpanded = false;
            }

            //// lose text focus/ clear text focus
            //Keyboard.ClearFocus();
            //Activate();

        }

        private void RestoreChatIconVisibilityDelayed_Tick(object sender, EventArgs e)
        {
            ChangeChatIconsVisibilityState(false);

            var dt = sender as System.Windows.Threading.DispatcherTimer;
            dt.Stop();
        }

        private void SearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            //TextBox tbox = sender as TextBox;

            //if (tbox.Text.Length != 0)
            //{
            //    tbox.Text = string.Empty;
            //}

            //SearchIconImage.Visibility = Visibility.Hidden;
        }

        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            //TextBox tbox = sender as TextBox;

            //if (tbox.Text.Length != 0)
            //{
            //    tbox.Text = string.Empty;
            //}

            //SearchIconImage.Visibility = Visibility.Visible;

            //RearrangeGrid(gameObjects);
            //RefreshGrid();
        }

        private Key[] searchKeys = new Key[36];

        private void AddSearchKeyPressEvents()
        {
            int pos = 34; // English alphabet starts at 44, numbers start at 34

            for (int i = 0; i < searchKeys.Length; i++)
            {
                searchKeys[i] = (Key)pos;
                pos++;
            }

            KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.Enter)
            {
                // Launch with CLOE
                Console.WriteLine("Launching with CLOE from Keyboard Ctrl + Enter");
                LaunchGame(HighlightedGame, true);
            }
            else if (e.Key == Key.Return)
            {
                Console.WriteLine("Key is Enter");
                if (HighlightedGame != null)
                {
                    //Console.WriteLine("Focused Game is: " + highlightedGame.DisplayName);
                    LaunchGame(HighlightedGame);
                }
            }

            if (e.Key == Key.Escape)
            {
                Console.WriteLine("Key is ESC");
                if (GridScrollViewer.IsMouseOver)
                {
                    Activate();
                }

                AnimateHighlight(false);
                ShrinkSearchBox();
            }

            if (e.Key == Key.Back)
            {
                Console.WriteLine("Key is Back");
                if (SearchTextBox.Text.Length != 0)
                {
                    if (GridScrollViewer.IsMouseOver)
                    {
                        Activate();
                    }

                    if (SearchTextBox.IsFocused == false)
                        SearchTextBox.Focus();
                }
            }

            if (e.Key == Key.Delete)
            {
                Console.WriteLine("Key is Delete");
                if (HighlightedGame != null)
                {
                    var result = OpenWindow.Notification($"If this is not a game, check the Blacklist checkbox so it doesn't get re-added on Rescan.\n\nYou can clear blacklisted apps from Settings", $"Remove {HighlightedGame.DockName}?", OpenWindow.NotificationWindowType.RemoveGame);

                    if (result == OpenWindow.NotificationResult.Yes)
                    {
                        HighlightedGame.Remove();
                    }

                    if (result == OpenWindow.NotificationResult.YesBlacklist)
                    {
                        HighlightedGame.Remove(true);
                    }
                }
            }

            if (searchKeys.Contains(e.Key))
            {
                Console.WriteLine("Key is containted");
                if (GridScrollViewer.IsMouseOver)
                {
                    Activate();
                }

                SearchTextBox.Visibility = Visibility.Visible;
                SearchTextBox.Focus();
                ExpandSearchBox();
            }
            else //unknown key pressed, do nothing (used to be start typing)
            {
                Console.WriteLine("Key is none");
                if (IsSearchBoxExpanded == false)
                {
                    e.Handled = true;
                }
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Was enabled
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                if (HighlightedGame != null && searchObjects.Count > 0)
                {
                    ArrowNavigationPressed(true, e.Key);
                    e.Handled = true;
                }
            }
        }

        public static void ArrowNavigationPressed(bool fromSearch = false, Key k = Key.None, bool loopNavigation = true)
        {
            LastInputSource = InputSource.Keyboard;

            if (HighlightedGame == null && GameObjects.Count > 0)
            {
                GameObjects[0].Focus();
            }

            if (IsSearchBoxExpanded)
            {
                Keyboard.ClearFocus();
                int currentIndex = searchObjects.IndexOf(HighlightedGame);
                Console.WriteLine("Current Index from search: " + currentIndex);

                if (Settings.Default.DockLocation <= 1)
                {
                    if (k == Key.Left)
                    {
                        if (currentIndex - 1 >= 0)
                        {
                            searchObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[searchObjects.Count - 1].Focus();
                            else
                                searchObjects[0].Focus();
                        }
                    }

                    if (k == Key.Right)
                    {
                        if (currentIndex + 1 < searchObjects.Count)
                        {
                            searchObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[0].Focus();
                            else
                                searchObjects[searchObjects.Count - 1].Focus();
                        }
                    }
                }
                else
                {
                    if (k == Key.Up)
                    {
                        if (currentIndex - 1 >= 0)
                        {
                            searchObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[searchObjects.Count - 1].Focus();
                            else
                                searchObjects[0].Focus();
                        }
                    }

                    if (k == Key.Down)
                    {
                        if (currentIndex + 1 < searchObjects.Count)
                        {
                            searchObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[0].Focus();
                            else
                                searchObjects[searchObjects.Count - 1].Focus();
                        }
                    }
                }


                //HighlightedGame.Focus();
            }
            else // Normal handled navigation
            {
                int currentIndex = GameObjects.IndexOf(HighlightedGame);

                if (Settings.Default.DockLocation <= 1)
                {
                    if (k == Key.Left)
                    {

                        if (currentIndex - 1 >= 0)
                        {
                            GameObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[GameObjects.Count - 1].Focus();
                            else
                                GameObjects[0].Focus();
                        }
                    }

                    if (k == Key.Right)
                    {
                        if (currentIndex + 1 < GameObjects.Count)
                        {
                            GameObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[0].Focus();
                            else
                                GameObjects[GameObjects.Count - 1].Focus();
                        }
                    }
                }
                else
                {
                    if (k == Key.Up)
                    {
                        if (currentIndex - 1 >= 0)
                        {
                            GameObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[GameObjects.Count - 1].Focus();
                            else
                                GameObjects[0].Focus();
                        }
                    }

                    if (k == Key.Down)
                    {
                        if (currentIndex + 1 < GameObjects.Count)
                        {
                            GameObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[0].Focus();
                            else
                                GameObjects[GameObjects.Count - 1].Focus();
                        }
                    }
                }
            }

            //// Not working
            //if (AppWindow.GridScrollViewer.IsMouseOver)
            //{
            //    AppWindow.Activate();
            //}
        }

        public static void ArrowNavigationPressedGlobal(bool fromSearch = false, System.Windows.Forms.Keys k = System.Windows.Forms.Keys.None, bool loopNavigation = true)
        {
            LastInputSource = InputSource.Keyboard;

            if (HighlightedGame == null && GameObjects.Count > 0)
            {
                GameObjects[0].Focus();
            }

            if (fromSearch)
            {
                Keyboard.ClearFocus();

                if (Settings.Default.DockLocation <= 1)
                {
                    if (k == System.Windows.Forms.Keys.Left)
                    {
                        int currentIndex = searchObjects.IndexOf(HighlightedGame);
                        if (currentIndex - 1 >= 0)
                        {
                            searchObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[searchObjects.Count - 1].Focus();
                            else
                                searchObjects[0].Focus();
                        }
                    }

                    if (k == System.Windows.Forms.Keys.Right)
                    {
                        int currentIndex = searchObjects.IndexOf(HighlightedGame);
                        if (currentIndex + 1 < searchObjects.Count)
                        {
                            searchObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[0].Focus();
                            else
                                searchObjects[searchObjects.Count - 1].Focus();
                        }
                    }
                }
                else
                {
                    if (k == System.Windows.Forms.Keys.Up)
                    {
                        int currentIndex = searchObjects.IndexOf(HighlightedGame);
                        if (currentIndex - 1 >= 0)
                        {
                            searchObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[searchObjects.Count - 1].Focus();
                            else
                                searchObjects[0].Focus();
                        }
                    }

                    if (k == System.Windows.Forms.Keys.Down)
                    {
                        int currentIndex = searchObjects.IndexOf(HighlightedGame);
                        if (currentIndex + 1 < searchObjects.Count)
                        {
                            searchObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                searchObjects[0].Focus();
                            else
                                searchObjects[searchObjects.Count - 1].Focus();
                        }
                    }
                }

                //HighlightedGame.Focus();
            }
            else // Normal handled navigation
            {
                if (Settings.Default.DockLocation <= 1)
                {
                    if (k == System.Windows.Forms.Keys.Left)
                    {
                        int currentIndex = GameObjects.IndexOf(HighlightedGame);
                        if (currentIndex - 1 >= 0)
                        {
                            GameObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[GameObjects.Count - 1].Focus();
                            else
                                GameObjects[0].Focus();
                        }
                    }

                    if (k == System.Windows.Forms.Keys.Right)
                    {
                        int currentIndex = GameObjects.IndexOf(HighlightedGame);
                        if (currentIndex + 1 < GameObjects.Count)
                        {
                            GameObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[0].Focus();
                            else
                                GameObjects[GameObjects.Count - 1].Focus();
                        }
                    }
                }
                else
                {
                    if (k == System.Windows.Forms.Keys.Up)
                    {
                        int currentIndex = GameObjects.IndexOf(HighlightedGame);
                        if (currentIndex - 1 >= 0)
                        {
                            GameObjects[currentIndex - 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[GameObjects.Count - 1].Focus();
                            else
                                GameObjects[0].Focus();
                        }
                    }

                    if (k == System.Windows.Forms.Keys.Down)
                    {
                        int currentIndex = GameObjects.IndexOf(HighlightedGame);
                        if (currentIndex + 1 < GameObjects.Count)
                        {
                            GameObjects[currentIndex + 1].Focus();
                        }
                        else
                        {
                            if (loopNavigation)
                                GameObjects[0].Focus();
                            else
                                GameObjects[GameObjects.Count - 1].Focus();
                        }
                    }
                }
            }

            //// Not working
            //if (AppWindow.GridScrollViewer.IsMouseOver)
            //{
            //    AppWindow.Activate();
            //}
        }

        public static void HighlightGameOnFocus()
        {
            if (HighlightedGame == null)
            {
                if (GameObjects.Count > 0)
                    GameObjects[0].Focus();
            }
            else
            {
                AppWindow.AnimateHighlight(true);
            }
        }

        //private void MainWindow_GlobalKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        //{
        //    //Console.WriteLine("Global Key Down: " + e.KeyCode);
        //    // Focus Fuzion from anywhere using Ctrl+` / Ctrl+Tilde / Ctrl+~
        //    if ((Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 && e.KeyCode.HasFlag(System.Windows.Forms.Keys.Oemtilde))
        //    {
        //        Console.WriteLine("Focus Fuzion from Ctrl+Tilde");
        //        TrayIcon.FocusFuzionOnClick();
        //        //e.Handled = true;
        //    }

        //    // Forward to shadowlaunch
        //    IdleTime.HookManager_KeyDown(sender, e);
        //}

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchTextBox.Text.Length != 0)
            {
                ShowSearchGrid(SearchTextBox.Text);
            }
            else
            {
                ShrinkSearchBox();
                //FocusPixel.Focus(); //was enabled
            }

            if (Settings.Default.SearchSteam && SearchTextBox.Text.Length > 1)
            {
                UpdateSearchResults(SearchTextBox.Text);
            }

        }

        private static List<Game> searchObjects = new List<Game>();

        private void ShowSearchGrid(string searchThis)
        {
            searchObjects.Clear();
            mainGrid.Children.Clear();

            for (int i = 0; i < GameObjects.Count; i++)
            {
                if (GameObjects[i].DisplayName.Contains(searchThis, StringComparison.OrdinalIgnoreCase)
                    || GameObjects[i].DockName.Contains(searchThis, StringComparison.OrdinalIgnoreCase))
                {
                    searchObjects.Add(GameObjects[i]);
                }
            }

            AnimateHighlight(true);
            RearrangeGrid(searchObjects);

            SelectBestMatchFromSearch(searchObjects);
        }

        private static Visibility discordButtonVisibilityState;
        private static Visibility steamFriendsButtonVisibilityState;

        private void ChangeChatIconsVisibilityState(bool hide)
        {
            // Doesn't work with setting toggle
            if (Settings.Default.DockLocation > 1)
                return;

            // Restore previous visibility
            if (hide == false)
            {
                discordLaunchButton.Visibility = discordButtonVisibilityState;
                steamFriendsLaunchButton.Visibility = steamFriendsButtonVisibilityState;
            }
            else // Hide
            {
                discordButtonVisibilityState = discordLaunchButton.Visibility;
                steamFriendsButtonVisibilityState = steamFriendsLaunchButton.Visibility;

                discordLaunchButton.Visibility = Visibility.Hidden;
                steamFriendsLaunchButton.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// Rearrange the grid to show search results, use only for Omni Search
        /// </summary>
        /// <param name="gameList"></param>
        private void RearrangeGrid(List<Game> gameList, bool reset = false)
        {
            mainGrid.Children.Clear();

            mainGrid.Children.Add(GameHighlightBorder);

            if (gameList.Count > 0)
            {
                for (int i = 0; i < gameList.Count; i++)
                {
                    mainGrid.Children.Add(gameList[i]);
                }

                // Not reverting to original list
                if (reset == false)
                    RefreshGrid(gameList);
            }
            else // Show Magnifying Glass
            {
                Image mglass = new Image
                {
                    Tag = "mglass",
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(@"Assets\128x\mglass.png", UriKind.Relative)),
                    Width = Settings.Default.StartupIconSize,
                    Height = Settings.Default.StartupIconSize,
                    Margin = DefaultGameMargins //new Thickness(1.5, 5, 1.5, 5);
                };

                mainGrid.Children.Add(mglass);

                RefreshGrid(gameList, mglass);
            }
        }

        private void SelectBestMatchFromSearch(List<Game> gameList)
        {
            Game bestMatch = gameList.FirstOrDefault(g => g.DisplayName.StartsWith(SearchTextBox.Text, StringComparison.OrdinalIgnoreCase));

            try
            {
                if (Settings.Default.DockLocation <= 1)
                {
                    if (bestMatch != null)
                    {
                        HighlightedGame = bestMatch;
                        Grid.SetColumn(GameHighlightBorder, Grid.GetColumn(bestMatch));
                    }
                    else
                    {
                        Grid.SetColumn(GameHighlightBorder, 0);
                        HighlightedGame = gameList[0];
                    }
                }
                else
                {
                    if (bestMatch != null)
                    {
                        HighlightedGame = bestMatch;
                        Grid.SetRow(GameHighlightBorder, Grid.GetRow(bestMatch));
                    }
                    else
                    {
                        Grid.SetRow(GameHighlightBorder, 0);
                        HighlightedGame = gameList[0];
                    }
                }

            }
            catch (Exception)
            {


            }
        }

        private async void UpdateSearchResults(string gameName)
        {
            List<Listing> steamSearchResults = new List<Listing>();

            try
            {
                steamSearchResults = await Task.Run(() => Query.Search(gameName)).ConfigureAwait(false);
            }
            catch (Exception)
            {

            }
            //kinguinSearchResults = await Task.Run(() => CheckKinguin(gameName)).ConfigureAwait(false);

            //if (kinguinSearchResults != null && kinguinSearchResults.Count != 0)
            //{
            //    Result1.Text = kinguinSearchResults[0];
            //    Result1_Price.Text = kinguinSearchResults[1] + "$";
            //    kinguinLink = kinguinSearchResults[2];
            //}

            if (steamSearchResults != null && steamSearchResults.Count != 0 && steamSearchResults[0] != null)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    PopulateSearchResultsGrid(steamSearchResults);
                }));

            }
        }

        private class BorderListing : Border
        {
            public enum StoreSource { Steam, Epic, GoG, Battlenet, Origin, Uplay }

            public string GameName { get; set; }

            private string _price;
            public string Price
            {
                get
                {
                    if (string.IsNullOrEmpty(_price))
                        return string.Empty;
                    else
                        return _price + "$";
                }

                set
                {
                    _price = value;
                }
            }
            public StoreSource Store { get; set; }
            public Border BackgroundBorder { get; set; }
            public string StoreLink { get; set; }
            public string SteamAppID { get; set; }
        }

        private const double auxGridMaxWidth = 451;
        private const double auxGridMaxHeight = 193;
        public const double searchResultsGridMaxWidth = 400;
        public const double searchResultRowHeight = 30;

        private void PopulateSearchResultsGrid(List<Listing> listings)
        {
            if (SearchTextBox.Text.Length != 0)
            {
                // Reset Grid
                SearchResultsGrid.Children.Clear();
                SearchResultsGrid.RowDefinitions.Clear();
                SearchResultsGrid.ColumnDefinitions.Clear();

                // Create Columns, width in percentages of searchResultsGridMaxWidth
                ColumnDefinition cd = new ColumnDefinition();
                cd.Width = new GridLength(searchResultsGridMaxWidth * 0.075, GridUnitType.Pixel);
                SearchResultsGrid.ColumnDefinitions.Add(cd);

                cd = new ColumnDefinition();
                cd.Width = new GridLength(searchResultsGridMaxWidth * 0.8, GridUnitType.Pixel);
                SearchResultsGrid.ColumnDefinitions.Add(cd);

                cd = new ColumnDefinition();
                cd.Width = new GridLength(searchResultsGridMaxWidth * 0.125, GridUnitType.Pixel);
                SearchResultsGrid.ColumnDefinitions.Add(cd);

                for (int i = 0; i < listings.Count; i++)
                {
                    // Background Border
                    Border bg = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
                    };

                    // Round First listing
                    if (i == 0)
                    {
                        bg.CornerRadius = new CornerRadius(10, 10, 0, 0);
                    }

                    // Round Last listing
                    if (i == listings.Count - 1)
                    {
                        bg.CornerRadius = new CornerRadius(0, 0, 10, 10);
                    }

                    // Create Row
                    RowDefinition rd = new RowDefinition
                    {
                        Height = new GridLength(searchResultRowHeight, GridUnitType.Pixel),
                    };

                    // Invisible Border for Mouse Events
                    BorderListing bl = new BorderListing
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                        GameName = listings[i].Name,
                        Price = listings[i].Price.ToString(),
                        Store = BorderListing.StoreSource.Steam,
                        StoreLink = listings[i].StoreLink,
                        SteamAppID = listings[i].AppId,
                        BackgroundBorder = bg
                    };

                    // Row Mouse Events
                    bl.MouseEnter += Result_MouseEnter;
                    bl.MouseLeave += Result_MouseLeave;
                    bl.MouseLeftButtonDown += Result_MouseLeftButtonDown;

                    // Game name
                    Label result = new Label
                    {
                        FontSize = 12d,
                        FontFamily = new FontFamily("Roboto Light"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Content = bl.GameName
                    };

                    // Game price
                    Label price = new Label
                    {
                        FontSize = 12d,
                        FontFamily = new FontFamily("Roboto Light"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Content = bl.Price
                    };

                    SearchResultsGrid.RowDefinitions.Add(rd);

                    // Source Store Image
                    Image img = new Image
                    {
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(@"/Assets/Steam/steamlogo64.png", UriKind.Relative)),
                        Width = 20d
                    };

                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                    // Add children to grid
                    SearchResultsGrid.Children.Add(bg);
                    SearchResultsGrid.Children.Add(img);
                    SearchResultsGrid.Children.Add(result);
                    SearchResultsGrid.Children.Add(price);
                    SearchResultsGrid.Children.Add(bl);

                    // Span background & border listing
                    Grid.SetColumnSpan(bl, 3);
                    Grid.SetColumnSpan(bg, 3);

                    // Set columns
                    Grid.SetColumn(bg, 0);
                    Grid.SetColumn(bl, 0);
                    Grid.SetColumn(img, 0);
                    Grid.SetColumn(result, 1);
                    Grid.SetColumn(price, 2);

                    // Set rows
                    Grid.SetRow(bg, i);
                    Grid.SetRow(bl, i);
                    Grid.SetRow(img, i);
                    Grid.SetRow(result, i);
                    Grid.SetRow(price, i);
                }

                CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
            }
        }

        private void Result_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BorderListing bl = sender as BorderListing;

            if (LauncherSpecific.Steam.Exists)
            {
                Process.Start("steam://store/" + bl.SteamAppID);
            }
            else
            {
                Process.Start("https://store.steampowered.com/app/" + bl.SteamAppID);
            }
        }

        private void Result_MouseLeave(object sender, MouseEventArgs e)
        {
            BorderListing bl = sender as BorderListing;
            bl.BackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

        private void Result_MouseEnter(object sender, MouseEventArgs e)
        {
            BorderListing bl = sender as BorderListing;
            bl.BackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(195, 255, 255, 255));
        }

        private void DealBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            //Border b = sender as Border;
            DealBackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(195, 255, 255, 255));
        }

        private void DealBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            //Border b = sender as Border;
            DealBackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

        private void HideDealButton_MouseEnter(object sender, MouseEventArgs e)
        {
            Image img = sender as Image;
            img.Opacity = 1d;
        }

        private void HideDealButton_MouseLeave(object sender, MouseEventArgs e)
        {
            Image img = sender as Image;
            img.Opacity = 0.6d;
        }

        private void HideDealButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DealGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.2d))));
            //DealGrid.Visibility = Visibility.Hidden;
        }

        private void DealHitTestBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start(DealLink.Content.ToString());
        }

        private void ShuffleDealsButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Image img = sender as Image;

            #region Rotate Animation

            Storyboard storyboard = new Storyboard();
            storyboard.Duration = TimeSpan.FromSeconds(0.2d);
            DoubleAnimation rotateAnimation = new DoubleAnimation()
            {
                From = 0,
                To = 180,
                Duration = storyboard.Duration
            };

            Storyboard.SetTarget(rotateAnimation, img);
            Storyboard.SetTargetProperty(rotateAnimation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            storyboard.Children.Add(rotateAnimation);

            img.BeginStoryboard(storyboard);

            #endregion

            //GameDeals.Deal d = GameDeals.DealChecker.GetNextDeal();

            //DealName.Content = d.Name;
            //DealPrice.Content = d.Price;
            //DealDiscountPercent.Content = d.DiscountPercent;
            //DealSource.Content = d.DealSource;
            //DealLink.Content = d.Link;
        }

        #region Drag Tests

        //https://stackoverflow.com/questions/3129443/wpf-4-drag-and-drop-with-visual-element-as-cursor

        #endregion

        private void mainWindow_Closed(object sender, EventArgs e)
        {
            //Console.WriteLine("MainWindow was closed");
            AnalyticsHelper.Current.LogEvent("Fuzion Closed", "Fuzion Event");
        }

        public static void RestartFuzion()
        {
            if (UniversalPlatform.Startup.IsUniversalPlatform)
            {
                ProcessStartInfo Info = new ProcessStartInfo();
                Info.Arguments = "/C choice /C Y /N /D Y /T 3 & START \"\" \"explorer.exe\" \"shell:appsFolder\\53755Tzar.Fuzion_cwp56ffgkw968!App\"";
                Info.WindowStyle = ProcessWindowStyle.Hidden;
                Info.CreateNoWindow = true;
                Info.FileName = "cmd.exe";
                Process.Start(Info);
                GracefulShutdown();
            }
            else
            {
                ProcessStartInfo Info = new ProcessStartInfo();
                Info.Arguments = "/C choice /C Y /N /D Y /T 3 & START \"\" \"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\"";
                Info.WindowStyle = ProcessWindowStyle.Hidden;
                Info.CreateNoWindow = true;
                Info.FileName = "cmd.exe";
                Process.Start(Info);
                GracefulShutdown();
            }
        }

        /// <summary>
        /// Removes KeyDown and MouseMove global hooks. Disables ShadowLaunch if currently running
        /// </summary>
        private void DetachGlobalMouseKeyboardEvents()
        {
            Native.ThreadedHook.DisableAllHooks();
            LauncherSpecific.ShadowLaunch.Disable();
        }

        public static void GracefulShutdown()
        {
            AnalyticsHelper.Current.LogEvent("Fuzion Shutdown", "Fuzion Event");
            Settings.Default.Save();
            TrayIcon.notifyIcon.Visible = false;
            TrayIcon.notifyIcon.Dispose();
            Scrolling.ScrollTimer.Dispose();
            AppWindow.DetachGlobalMouseKeyboardEvents();
            Application.Current.Shutdown();
        }

        private void DragBorder_DragLeave(object sender, DragEventArgs e)
        {
            CustomUIElements.DragAdornerInitializer.ClearAdorner(true);
            //if (!GridScrollViewer.IsMouseOver)

            //else
            //    Console.WriteLine("Mouse is still over scrollviewer");
        }

        private void mainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            Console.WriteLine("DPI Change occured with new DPIx: " + e.NewDpi.DpiScaleX);
            if (AppWindow.IsLoaded) // dpichanged may run before the window has loaded and break the app
                CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

        private void mainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                LastInputSource = InputSource.Keyboard;

                // Arrow nav override no search
                ArrowNavigationPressed(false, e.Key, false);
                e.Handled = true;
            }
        }

        private void DragBorder_PreviewGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            //Console.WriteLine("Giving Feedback");
            //Mouse.SetCursor(Cursors.Hand);
            //e.Handled = true;
        }

        private void GridScrollViewer_DragEnter(object sender, DragEventArgs e)
        {
            // If not rearranging the game grid because otherwise it'll lock grid rearrange movement
            if (!isRearrangingGrid && DragBorder.Visibility == Visibility.Collapsed)
            {
                DragBorder.Visibility = Visibility.Visible;
            }
        }

        private void GridScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //// Was enabled
            //if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            //{
            //    //Console.WriteLine("Key is Arrow from scrollviewer");
            //    LastInputSource = InputSource.Keyboard;
            //    ArrowNavigationPressed(false, e.Key, false);
            //    e.Handled = true;
            //}
        }

        //variables to store the offset values
        double relX;
        double relY;

        private void GridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //ScrollViewer scroll = sender as ScrollViewer;
            ////see if the content size is changed
            //if (e.ExtentWidthChange != 0 || e.ExtentHeightChange != 0)
            //{
            //    //calculate and set accordingly
            //    scroll.ScrollToHorizontalOffset(CalculateOffset(e.ExtentWidth, e.ViewportWidth, scroll.ScrollableWidth, relX));
            //    scroll.ScrollToVerticalOffset(CalculateOffset(e.ExtentHeight, e.ViewportHeight, scroll.ScrollableHeight, relY));
            //}
            //else
            //{
            //    //store the relative values if normal scroll

            //    relX = (e.HorizontalOffset + 0.5 * e.ViewportWidth) / e.ExtentWidth;
            //    relY = (e.VerticalOffset + 0.5 * e.ViewportHeight) / e.ExtentHeight;
            //    Console.WriteLine("relX " + relX);
            //    Console.WriteLine("relY " + relY);
            //}

            //if (Settings.Default.DockLocation <= 1)
            //{
            //    ToggleScrollViewerOffsets((int)GridScrollViewer.ScrollableWidth);
            //    ToggleScrollViewerEdgeFade((int)GridScrollViewer.ActualWidth);
            //}
            //else
            //{
            //    ToggleScrollViewerOffsets((int)GridScrollViewer.ScrollableHeight);
            //    ToggleScrollViewerEdgeFade((int)GridScrollViewer.ActualHeight);
            //}


        }

        private static double CalculateOffset(double extent, double viewPort, double scrollWidth, double relBefore)
        {
            //calculate the new offset
            double offset = relBefore * extent - 0.5 * viewPort;
            //see if it is negative because of initial values
            if (offset < 0)
            {
                //center the content
                //this can be set to 0 if center by default is not needed
                offset = 0.5 * scrollWidth;
            }
            return offset;
        }

        static DispatcherTimer windowActivationTimer = GetWindowActivationTimer();

        static DispatcherTimer GetWindowActivationTimer()
        {
            var res = new DispatcherTimer()
            {
                Interval = TimeSpan.FromTicks(10)
            };

            res.Tick += WindowActivationTimer_Tick;
            return res;
        }

        private static void WindowActivationTimer_Tick(object sender, EventArgs e)
        {
            if (!AppWindow.IsKeyboardFocusWithin)
            {
                AppWindow.MainWindow_Deactivated(null, null);
            }

            windowActivationTimer.Stop();
        }

        private void mainWindow_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Due to the new way Fuzion parents to a desktop window, it doesn't call Activated events
            // We're going to manually pass Activated events using global hook for keyboard and mouse
            if (IsKeyboardFocusWithin)
            {
                MainWindow_Activated(null, null);
            }
            else
            {
                if (IsSearchBoxExpanded)
                {
                    // quick and dirty dispatcher solution
                    // wait 10 ticks
                    windowActivationTimer.Start();
                }
                else
                {
                    MainWindow_Deactivated(null, null);
                }
            }

        }
    }
}
