using Fuzion.WindowsManager;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Fuzion.MainWindow;
using static Fuzion.SettingsManager.GeneralSettings;
using Fuzion.Properties;
using static Fuzion.Programs.ProgramManager;
using System.Deployment.Application;
using System.IO;
using System.Globalization;
using Fuzion.Update;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Fuzion.Native;
using System.Drawing.Drawing2D;
using System.Drawing;

namespace Fuzion
{

    #region Converters
    public class BoolToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value == true) ? 0 : 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((int)value == 0) ? true : false;
        }
    }
    #endregion

    public class DockLocationEventArgs : EventArgs
    {
        public DockLocationEventArgs(bool top)
        {
            Top = top;
        }

        public bool Top { get; private set; }
    }

    public class IconSizeChangedEventArgs : EventArgs
    {
        public IconSizeChangedEventArgs(double size)
        {
            Size = size;
        }

        public double Size { get; private set; }
    }

    public partial class SettingsWindow : Window
    {

        #region New Blur - https://tvc-16.science/mica-wpf.html MICA/ACRYLIC - requires: Windows 11 build 22523 or newer .NET 6 Windows desktop runtime or newer.
       // [DllImport("dwmapi.dll")]
       // public static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

       // [DllImport("DwmApi.dll")]
       // static extern int DwmExtendFrameIntoClientArea(
       //  IntPtr hwnd,
       //  ref MARGINS pMarInset);

       // public static int ExtendFrame(IntPtr hwnd, MARGINS margins)
       // => DwmExtendFrameIntoClientArea(hwnd, ref margins);
       // /*
       //[Flags]
       //enum DWM_SYSTEMBACKDROP_TYPE
       //{
       //    DWMSBT_MAINWINDOW = 2, // Mica
       //    DWMSBT_TRANSIENTWINDOW = 3, // Acrylic
       //    DWMSBT_TABBEDWINDOW = 4 // Tabbed
       //}
       //*/
       // [StructLayout(LayoutKind.Sequential)]
       // public struct MARGINS
       // {
       //     public int cxLeftWidth;      // width of left border that retains its size
       //     public int cxRightWidth;     // width of right border that retains its size
       //     public int cyTopHeight;      // height of top border that retains its size
       //     public int cyBottomHeight;   // height of bottom border that retains its size
       // };

       // [Flags]
       // public enum DwmWindowAttribute : uint
       // {
       //     DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
       //     DWMWA_MICA_EFFECT = 1029,
       //     DWMWA_WINDOW_CORNER_PREFERENCE = 33,
       //     DWMWA_SYSTEMBACKDROP_TYPE = 38
       // }

       // public static void UpdateStyleAttributes()
       // {
       //     int trueValue = 0x01;
       //     int acrylic = 3;
       //     var handle = new WindowInteropHelper(Instance).Handle;
       //     DwmSetWindowAttribute(handle, DwmWindowAttribute.DWMWA_SYSTEMBACKDROP_TYPE,ref acrylic, Marshal.SizeOf(typeof(int)));
       //     //DwmSetWindowAttribute(hwnd.Handle, DwmWindowAttribute.DWMWA_WINDOW_CORNER_PREFERENCE, ref trueValue, Marshal.SizeOf(typeof(int)));
       // }
       // private void RefreshFrame()
       // {
       //     IntPtr mainWindowPtr = new WindowInteropHelper(this).Handle;
       //     HwndSource mainWindowSrc = HwndSource.FromHwnd(mainWindowPtr);
       //     mainWindowSrc.CompositionTarget.BackgroundColor = System.Windows.Media.Color.FromArgb(0, 0, 0, 0);

       //     System.Drawing.Graphics desktop = System.Drawing.Graphics.FromHwnd(mainWindowPtr);
       //     float DesktopDpiX = desktop.DpiX;

       //     MARGINS margins = new MARGINS();
       //     margins.cxLeftWidth = Convert.ToInt32(5 * (DesktopDpiX / 96));
       //     margins.cxRightWidth = Convert.ToInt32(5 * (DesktopDpiX / 96));
       //     margins.cyTopHeight = Convert.ToInt32(((int)ActualHeight + 5) * (DesktopDpiX / 96));
       //     margins.cyBottomHeight = Convert.ToInt32(5 * (DesktopDpiX / 96));

       //     ExtendFrame(mainWindowSrc.Handle, margins);
       // }

        #endregion

        #region Blur
        //[DllImport("user32.dll")]
        //internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        //[StructLayout(LayoutKind.Sequential)]
        //internal struct WindowCompositionAttributeData
        //{
        //    public WindowCompositionAttribute Attribute;
        //    public IntPtr Data;
        //    public int SizeOfData;
        //}

        //internal enum WindowCompositionAttribute
        //{
        //    // ...
        //    WCA_ACCENT_POLICY = 19
        //    // ...
        //}

        //internal enum AccentState
        //{
        //    ACCENT_DISABLED = 0,
        //    ACCENT_ENABLE_GRADIENT = 1,
        //    ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        //    ACCENT_ENABLE_BLURBEHIND = 3,
        //    ACCENT_INVALID_STATE = 4
        //}

        //[StructLayout(LayoutKind.Sequential)]
        //internal struct AccentPolicy
        //{
        //    public AccentState AccentState;
        //    public int AccentFlags;
        //    public int GradientColor;
        //    public int AnimationId;
        //}
        //internal void EnableBlur()
        //{
        //    var windowHelper = new WindowInteropHelper(this);

        //    var accent = new AccentPolicy();
        //    var accentStructSize = Marshal.SizeOf(accent);
        //    accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;

        //    var accentPtr = Marshal.AllocHGlobal(accentStructSize);
        //    Marshal.StructureToPtr(accent, accentPtr, false);

        //    var data = new WindowCompositionAttributeData
        //    {
        //        Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
        //        SizeOfData = accentStructSize,
        //        Data = accentPtr
        //    };

        //    _ = SetWindowCompositionAttribute(windowHelper.Handle, ref data);

        //    Marshal.FreeHGlobal(accentPtr);
        //}
        #endregion

        #region Blur with Corners
        // Doesn't work in win 10
        void ShapeBlurBehind()
        {
            if (!NativeMethods.DwmIsCompositionEnabled())
                return;

            Console.WriteLine("Blurring in shape");
            var hwnd = new WindowInteropHelper(this).Handle;

            var hwndSource = HwndSource.FromHwnd(hwnd);
            var sizeFactor = hwndSource.CompositionTarget.TransformToDevice.Transform(new Vector(1.0, 1.0));

            //Background = System.Windows.Media.Brushes.Transparent;
            hwndSource.CompositionTarget.BackgroundColor = Colors.Transparent;

            using (var path = new GraphicsPath())
            {
                //path.AddEllipse(0, 0, (int)(ActualWidth * sizeFactor.X), (int)(ActualHeight * sizeFactor.Y));
                path.AddRectangle(new System.Drawing.Rectangle(0, 0, (int)ActualWidth, (int)ActualHeight));

                using (var region = new Region(path))
                using (var graphics = Graphics.FromHwnd(hwnd))
                {
                    var hRgn = region.GetHrgn(graphics);

                    var blur = new NativeMethods.DWM_BLURBEHIND
                    {
                        dwFlags = NativeMethods.DWM_BB.ENABLE | NativeMethods.DWM_BB.BLURREGION | NativeMethods.DWM_BB.TRANSITIONONMAXIMIZED,
                        fEnable = true,
                        hRgnBlur = hRgn,
                        fTransitionOnMaximized = true
                    };

                    NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref blur);

                    region.ReleaseHrgn(hRgn);
                }
            }
        }

        //private void SettingsWindowFrontEnd_SourceInitialized(object sender, EventArgs e)
        //{
        //    //ShapeBlurBehind();
        //}
        #endregion

        private bool loaded;
        public static SettingsWindow Instance;
        public event EventHandler<DockLocationEventArgs> SetDockLocationEvent;
        public event EventHandler<IconSizeChangedEventArgs> ChangeIconsSizeEvent;

        public System.Collections.ObjectModel.ObservableCollection<int> ActiveMonitorItemSource { get; } = new System.Collections.ObjectModel.ObservableCollection<int>(Position.Monitors.ScreenIndexes);
        public System.Collections.ObjectModel.ObservableCollection<string> ObservableBlacklist { get; private set; } = new System.Collections.ObjectModel.ObservableCollection<string>(SettingsManager.Blacklist.Load());

        const string DynamicContentLink = "https://fuzion.gg/dynamic/meta.json";
        const string SymbolCheckerLink = "https://fuzion.gg/dynamic/sym.json";
        private static int CurrentDynamicImageIndex = 0;
        private class DynamicImage
        {
            public string imageSource;
            public string clickLink;
            public BitmapImage bmp;
        }

        // Always shown as the first card, regardless of whether the remote content fetch below
        // succeeds - bundled into the app itself rather than downloaded, so it can't go missing.
        private static readonly DynamicImage KoFiSupportCard = new DynamicImage
        {
            clickLink = "https://ko-fi.com/fuzion",
            // Frozen: this BitmapImage is built the first time this static field is touched,
            // which isn't guaranteed to be the UI thread (C# doesn't pin static initializer
            // timing to a specific thread). BitmapImage is a DispatcherObject and throws on
            // cross-thread access unless frozen, which makes it thread-independent instead.
            bmp = Icons.BitmapTools.ImageFromPath("pack://application:,,,/Assets/ko-fi-support-card.png", freeze: true)
        };

        private static List<DynamicImage> DynamicImages = new List<DynamicImage> { KoFiSupportCard };
        private static DynamicImage CurrentDynamicImage;
        private static List<DynamicImage> LastDynamicImageList;
        private static string dimagesSavePath = CreateDimageSavePath();
        private static string contentUpdateSymbol;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Instance = this;

            //ActiveMonitor.DataContext = this;
            //ActiveMonitor.ItemsSource = ActiveMonitorItemSource;
            //SetDogeQuote();

            Console.WriteLine("Activemonitor itemsource is "+ActiveMonitor.ItemsSource); 
            //Console.WriteLine("ActiveMonitorItemSource count "+ActiveMonitorItemSource.Count);
            shadowLaunchLaunchers = FillExistingLaunchersList();
            //Console.WriteLine("Shadow launchers count "+shadowLaunchLaunchers.Count);
            allShadowLaunchers = FillAllLaunchersList();
            loaded = true;
            LoadSettings();

            VersionTextBox.Text = GetVersion();

            if (UniversalPlatform.Startup.IsUniversalPlatform == false)
            {
                ResetLocalDatabase.Visibility = Visibility.Visible;
            }

            _ = Task.Run(() => GetDynamicImages());
            SetDynamicContent();
            //EnableBlur();

            //// Test area
            //System.Windows.Threading.DispatcherTimer testTimer = new System.Windows.Threading.DispatcherTimer();
            //testTimer.Tick += TestTimer_Tick;
            //testTimer.Interval = TimeSpan.FromSeconds(1d);
            //testTimer.Start();
        }

        //private void TestTimer_Tick(object sender, EventArgs e)
        //{
        //    TestShowLeftLabel.Content = AppWindow.Left.ToString();
        //    NativeMethods.RECT rect = NativeMethods.GetWindowRectangle(AppWindow);
        //    NativeMethods.RECT parentRect = NativeMethods.GetWindowRectangle(NativeMethods.IntermediateWorkerWPointer);

        //    TestShowRectLabel.Content = $"Left {rect.Left} Right {rect.Right} Top {rect.Top} Bottom {rect.Bottom}";
        //    TestShowParentRectLabel.Content = $"Parent Left {parentRect.Left} Right {parentRect.Right} Top {parentRect.Top} Bottom {parentRect.Bottom}";
        //    SysParamWorkingAreaLabel.Content = $"SysParam Left {SystemParameters.VirtualScreenLeft} Right {SystemParameters.VirtualScreenWidth} Top {SystemParameters.VirtualScreenTop} Bottom {SystemParameters.VirtualScreenHeight}";
        //}
        private static string CreateDimageSavePath()
        {
            string path = System.IO.Path.Combine(DefaultAssetPath, "dimages");
            Directory.CreateDirectory(path);
            return path;
        }

        public static void GetDynamicImages()
        {
            List<DynamicImage> res = new List<DynamicImage>();
            string updateSymbol;
            try
            {
                string json;
                using (System.Net.WebClient wc = new System.Net.WebClient())
                {
                    updateSymbol = wc.DownloadString(SymbolCheckerLink);

                    Console.WriteLine("Current Update SYM: " + contentUpdateSymbol);
                    Console.WriteLine("Web Update SYM: " + updateSymbol);
                    // If json hasn't changed, don't update
                    if (contentUpdateSymbol == updateSymbol)
                    {
                        wc.Dispose();
                        return;
                    }

                    json = wc.DownloadString(DynamicContentLink);
                    // ASYNC version
                    //var uriFromLink = new Uri(DynamicContentLink);
                    //json = await wc.DownloadStringTaskAsync(uriFromLink).ConfigureAwait(false);
                }
                res = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DynamicImage>>(json);

                Console.WriteLine("Loading Dynamic Content");

                Console.WriteLine("Updating Dynamic Content - Has Changed");

                //store images in vars
                for (int i = 0; i < res.Count; i++)
                {
                    string cachedPath = System.IO.Path.Combine(dimagesSavePath, i + ".png");
                    using (System.Net.WebClient wc = new System.Net.WebClient())
                    {
                        wc.DownloadFile(res[i].imageSource, cachedPath);
                    }
                    // Frozen: loaded on this background thread, but displayed later on the UI
                    // thread via the Dispatcher.Invoke below - BitmapImage throws on cross-thread
                    // access unless frozen.
                    res[i].bmp = Icons.BitmapTools.ImageFromPath(cachedPath, freeze: true);
                }

                // Ko-fi always leads the rotation; remote cards follow.
                DynamicImages = new List<DynamicImage> { KoFiSupportCard }.Concat(res).ToList();
                contentUpdateSymbol = updateSymbol;

                // Runs on a background thread (see Window_Loaded's Task.Run) and nothing else
                // re-triggers a UI refresh after the initial call, so push it now that new
                // content is ready.
                Instance?.Dispatcher.Invoke(() => SetDynamicContent(CurrentDynamicImageIndex));
            }
            catch (Exception)
            {
                //throw;
                Console.WriteLine("Failed to Load Dynamic Content");
            }
        }

        /// <summary>
        /// Displays the dynamic image at the given index. Each DynamicImage's bmp is already
        /// loaded (hardcoded for the Ko-fi card, or right after download for remote ones), so
        /// this only selects and shows it. CALL with Dispatcher only.
        /// </summary>
        /// <param name="index"></param>
        private static void SetDynamicContent(int index = 0)
        {
            if (DynamicImages.Count > 0)
            {
                // Show arrows
                if(DynamicImages.Count > 1)
                {
                    Instance.NextDynamicImageButton.Visibility = Visibility.Visible;
                    Instance.PrevDynamicImageButton.Visibility = Visibility.Visible;
                }
                else
                {
                    Instance.NextDynamicImageButton.Visibility = Visibility.Hidden;
                    Instance.PrevDynamicImageButton.Visibility = Visibility.Hidden;
                }

                if (index >= DynamicImages.Count)
                {
                    index = 0;
                }

                if (index < 0)
                {
                    index = DynamicImages.Count - 1;
                }

                CurrentDynamicImage = DynamicImages[index];
                CurrentDynamicImageIndex = index;
                Console.WriteLine("Current index set: " + CurrentDynamicImageIndex);
                //DynamicImageContent.Source = Icons.BitmapTools.ImageFromPath(CurrentDynamicImage.imageSource);

                Instance.DynamicImageContent.Source = DynamicImages[index].bmp;
            }
        }

        private void DynamicImageContent_MouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start(CurrentDynamicImage.clickLink);
        }

        private void PrevDynamicImageButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetDynamicContent(CurrentDynamicImageIndex - 1);
            });
        }

        private void NextDynamicImageButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetDynamicContent(CurrentDynamicImageIndex + 1);
            });
        }

        private static readonly string[] dogeQuotes = new string[]
        {
            "Toss a Dogecoin to your Fuzion.",
            "The right Doge in the wrong place can make all the difference in the world.",
            "Bring me a Doge, and I'll show you a Doge!",
            "No gods or kings. Only Doge.",
            "Nothing is true, everything is Doge.",
            "It’s dangerous to go alone, take this doge!",
            "Doge. Doge never changes."
        };

        private static void SetDogeQuote()
        {
            //Random rand = new Random();
            
            //Instance.DogeCoinLabel.Text = dogeQuotes[rand.Next(0, dogeQuotes.Length)];
        }

        private static string GetVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            //System.Diagnostics.FileVersionInfo vInfo = 
            return System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString() + " b";
        }

        private void LoadSettings()
        {
            LoadShadowLaunchSettings();

            //// Active monitor selection
            //ActiveMonitor.ItemsSource = Position.Monitors.ScreenIndexes;
            //ActiveMonitor.SelectedIndex = Settings.Default.ActiveScreenIndex;

            if (Settings.Default.IconsPerGame == 5)
            {
                IconRelevance.SelectedIndex = 0;
            }

            if (Settings.Default.IconsPerGame == 7)
            {
                IconRelevance.SelectedIndex = 1;
            }

            if (Settings.Default.IconsPerGame == 10)
            {
                IconRelevance.SelectedIndex = 2;
            }

            //Set slider max value and load
            iconSizeSlider.Value = Settings.Default.StartupIconSize;

            //Load Settings

            dockLocation.SelectedIndex = Settings.Default.DockLocation;

            // Launch on startup checks
            StartupCheckBox.IsEnabled = true;
            // Async check for startup state would be better, but for now relying on settings or just enabling manual toggle.
            // Ideally: StartupCheckBox.IsChecked = await UniversalPlatform.Startup.GetCurrentStartupState(); 
            // But LoadSettings is synchronous. 
            // We can fire and forget or just let user toggle. 
            // Existing code synced Settings.Default.LaunchOnStartup.

            // if (UniversalPlatform.Startup.IsUniversalPlatform) block removed.

            // Launch click count
            if (Settings.Default.LaunchClickCount == 1)
            {
                launchClickCountDropdown.SelectedIndex = 0;
            }
            else
            {
                launchClickCountDropdown.SelectedIndex = 1;
            }

            // Reload Blacklist
            SettingsManager.Blacklist.ReloadList();

            // Toggle Blacklist visibility
            ToggleBlacklistRemoveButtonVisibility();

            // Initialize Background Settings Visibility
            if (Settings.Default.BackgroundAutoSize)
            {
                BackgroundWidthGrid.Visibility = Visibility.Collapsed;
                BackgroundHeightGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackgroundWidthGrid.Visibility = Visibility.Visible;
                BackgroundHeightGrid.Visibility = Visibility.Visible;
            }
        }

        private static void InstallUpdateSyncWithInfo()
        {
            UpdateCheckInfo info = null;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                try
                {
                    info = ad.CheckForDetailedUpdate();

                }
                catch (DeploymentDownloadException dde)
                {
                    OpenWindow.Notification("The new version of the application cannot be downloaded at this time. \n\nPlease check your network connection, or try again later. Error: " + dde.Message);
                    return;
                }
                catch (InvalidDeploymentException ide)
                {
                    OpenWindow.Notification("Cannot check for a new version of the application. The ClickOnce deployment is corrupt. Please redeploy the application and try again. Error: " + ide.Message);
                    return;
                }
                catch (InvalidOperationException ioe)
                {
                    OpenWindow.Notification("This application cannot be updated. It is likely not a ClickOnce application. Error: " + ioe.Message);
                    return;
                }

                if (info.UpdateAvailable)
                {
                    bool doUpdate = true;

                    if (!info.IsUpdateRequired)
                    {
                        var dr = OpenWindow.Notification("An update is available. Would you like to update Fuzion now?", "Update Available", OpenWindow.NotificationWindowType.YesNo);
                        if (!(OpenWindow.NotificationResult.Yes == dr))
                        {
                            doUpdate = false;
                        }
                    }
                    else
                    {
                        // Display a message that the app MUST reboot. Display the minimum required version.
                        OpenWindow.Notification("This application has detected a mandatory update from your current " +
                            "version to version " + info.MinimumRequiredVersion.ToString() +
                            ". The application will now install the update and close.",
                            "Update Available", OpenWindow.NotificationWindowType.Ok);
                    }

                    if (doUpdate)
                    {
                        try
                        {
                            ad.Update();
                            OpenWindow.Notification("The application has been upgraded and will now close, please re-open Fuzion");
                            //System.Windows.Forms.Application.Restart();
                            Icons.TrayIcon.notifyIcon.Visible = false;
                            Icons.TrayIcon.notifyIcon.Dispose();
                            Application.Current.Shutdown();
                        }
                        catch (DeploymentDownloadException dde)
                        {
                            OpenWindow.Notification("Cannot install the latest version of the application. \n\nPlease check your network connection, or try again later. Error: " + dde);
                            return;
                        }
                    }
                }
                else
                {
                    OpenWindow.Notification("You're up to date!", "No updates");
                }
            }
        }

        #region Shadow Launch
        private List<CheckBox> shadowLaunchLaunchers;
        private List<CheckBox> allShadowLaunchers;

        private List<CheckBox> FillExistingLaunchersList()
        {
            List<CheckBox> result = new List<CheckBox>();

            if (LauncherSpecific.Steam.Exists)
                result.Add(SteamEnabled);

            if (LauncherSpecific.Origin.Exists)
                result.Add(OriginEnabled);

            if (LauncherSpecific.BattleNet.Exists)
                result.Add(BattleNetEnabled);

            if (LauncherSpecific.EpicGames.Exists)
                result.Add(EpicEnabled);

            if (LauncherSpecific.GOG.Exists)
                result.Add(GoGEnabled);

            if (LauncherSpecific.Uplay.Exists)
                result.Add(UplayEnabled);

            return result;
        }

        private List<CheckBox> FillAllLaunchersList()
        {
            return new List<CheckBox>()
            {
                SteamEnabled,
                OriginEnabled,
                BattleNetEnabled,
                EpicEnabled,
                GoGEnabled,
                UplayEnabled
            };
        }

        private void LoadShadowLaunchSettings()
        {
            if (shadowLaunchLaunchers.Count == 0 || shadowLaunchLaunchers == null)
            {
                //EnableShadowLaunch.IsEnabled = false;

                for (int i = 0; i < allShadowLaunchers.Count; i++)
                {
                    ActivateSLCheckBox(allShadowLaunchers[i], false);
                }
            }
            else
            {
                //EnableShadowLaunch.IsEnabled = true;

                Console.WriteLine("OBSOLETE: Shadow launch state from load: " + Settings.Default.IsShadowLaunchEnabled);

                for (int i = 0; i < allShadowLaunchers.Count; i++)
                {
                    ActivateSLCheckBox(allShadowLaunchers[i], false);
                }

                for (int i = 0; i < shadowLaunchLaunchers.Count; i++)
                {
                    Grid.SetRow(shadowLaunchLaunchers[i], i);
                    ActivateSLCheckBox(shadowLaunchLaunchers[i], true);
                }
            }
        }



        private static void ActivateSLCheckBox(CheckBox cBox, bool active)
        {
            if (active)
            {
                // Also enable shadow launch for this launcher
                //cBox.Visibility = Visibility.Visible;
                cBox.IsEnabled = true;
            }
            else
            {
                // Also DISABLE shadow launch for every missing launcher
                //cBox.Visibility = Visibility.Hidden;
                cBox.IsEnabled = false;
            }
        }

        #endregion

        public static int GetMaxIconSize()
        {
            return Convert.ToInt32(Math.Ceiling(SystemParameters.PrimaryScreenWidth / (GameObjects.Count + 2d))); // 2 is an offset which adds space at each end of the dock
        }

        private void StartupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cbox = sender as CheckBox;

            if (cbox.IsChecked == true)
            {
                if (UniversalPlatform.Startup.IsUniversalPlatform == false)
                {
                    MainWindow.ManageStartupRegistryKey(true);

                }
                else
                {
                    UniversalPlatform.Startup.UpdateStartupState(true);
                }
            }
            else
            {
                if (UniversalPlatform.Startup.IsUniversalPlatform == false)
                {
                    ManageStartupRegistryKey(false);

                }
                else
                {
                    UniversalPlatform.Startup.UpdateStartupState(false);
                }
            }

            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
        }


        private void Window_Drag(object sender, MouseButtonEventArgs e)
        {
            if (CloseButton.IsMouseOver == false && e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void StickCheckBox_Click(object sender, RoutedEventArgs e)
        {
            //if (StickCheckBox.IsChecked == true)
            //{
            //    OpenWindow.Notification("Fuzion will attempt to restart to enable 'Stick to desktop'", "Restarting");
            //    Native.NativeMethods.SetOnDesktop(AppWindow, true, true); // also restart
            //}
            //else
            //{
            //    Native.NativeMethods.SetOnDesktop(AppWindow, false);
            //}

            //Settings.Default.Save();
            //UniversalPlatform.OnUpdate.UpdateUWPSettings();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (loaded)
            {
                Settings.Default.StartupIconSize = e.NewValue;
                // Update original icon size
                if (!MainWindow.IsZoomActive)
                {
                    Settings.Default.OriginalIconSize = e.NewValue;
                }
                ChangeIconsSizeEvent(this, new IconSizeChangedEventArgs(e.NewValue));
            }
        }

        private void DockLocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.Default.DockLocation = dockLocation.SelectedIndex;
            SetDockLocationEvent(this, new DockLocationEventArgs(true));

            //if (dockLocation.SelectedIndex == 0)
            //{
            //    Properties.Settings.Default.LocationTop = true; // obsolete
            //    Settings.Default.DockLocation = 0;
            //    SetDockLocationEvent(this, new DockLocationEventArgs(true));
            //}
            //else
            //{
            //    Properties.Settings.Default.LocationTop = false; // obsolete
            //    Settings.Default.DockLocation = 2;
            //    SetDockLocationEvent(this, new DockLocationEventArgs(false));
            //}
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
            OpenWindowsManager.WindowReferenceControl("Settings", this, OpenWindowsManager.R.Remove);
        }

        private void LaunchClickCountDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (launchClickCountDropdown.SelectedIndex == 0)
            {
                Properties.Settings.Default.LaunchClickCount = 1;
            }
            else
            {
                Properties.Settings.Default.LaunchClickCount = 2;
            }
        }

        private void IdleTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cbox = sender as ComboBox;
            Console.WriteLine("Idle Time From ComboBox: " + cbox.SelectedItem.ToString());
            Console.WriteLine("Idle Time From Settings: " + Settings.Default.IdleTimeSL);
        }

        #region Shadow Launcher Click Events
        private void ShadowLauncher_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
        }

        #endregion

        private void ChatBarCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cbox = sender as CheckBox;

            if (cbox.IsChecked == true)
            {
                AppWindow.SetChatBarVisibility(true);
            }
            else
            {
                AppWindow.SetChatBarVisibility(false);
            }

            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
        }

        private void ShowUnhandledExceptions_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cbox = sender as CheckBox;

            if (cbox.IsChecked == true)
            {
                Debug.CatchUnhandledExceptions.EnableMessageBoxOnUnhandledException(true);
            }
            else
            {
                Debug.CatchUnhandledExceptions.EnableMessageBoxOnUnhandledException(false);
            }

            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
        }

        private void EnableShadowLaunch_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cbox = sender as CheckBox;

            if (cbox.IsChecked == true)
            {
                for (int i = 0; i < allShadowLaunchers.Count; i++)
                {
                    ActivateSLCheckBox(allShadowLaunchers[i], false);
                }

                for (int i = 0; i < shadowLaunchLaunchers.Count; i++)
                {
                    Grid.SetColumn(shadowLaunchLaunchers[i], i);
                    ActivateSLCheckBox(shadowLaunchLaunchers[i], true);
                }

                LauncherSpecific.ShadowLaunch.UpdateState();
            }
            else
            {
                for (int i = 0; i < allShadowLaunchers.Count; i++)
                {
                    ActivateSLCheckBox(allShadowLaunchers[i], false);
                }

                LauncherSpecific.ShadowLaunch.UpdateState();
            }

            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
        }

        // Icon relevance is now locked to 5 until icon database is built
        private void IconRelevance_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox s = sender as ComboBox;

            // Low
            if (s.SelectedIndex == 0)
            {
                Settings.Default.IconsPerGame = 5;
            }

            // Medium
            if (s.SelectedIndex == 1)
            {
                Settings.Default.IconsPerGame = 5;
            }

            // High
            if (s.SelectedIndex == 2)
            {
                Settings.Default.IconsPerGame = 5;
            }

            Settings.Default.Save();
        }

        private void SettingsWindowFrontEnd_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CloseButton.IsMouseOver)
            {
                Close();
            }
        }

        private void OutlineColorSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Console.WriteLine("Game Outline Changed: "+Settings.Default.DarkGameOutline);
            if (Settings.Default.DarkGameOutline)
            {
                Application.Current.Resources["Game.ShadowColor"] = System.Windows.Media.Color.FromArgb(255, 40, 40, 40);
            } else
            {
                Application.Current.Resources["Game.ShadowColor"] = System.Windows.Media.Color.FromArgb(255, 255, 255, 255);
            }

            AppWindow.UpdateGameHighlightBrush();
        }

        private void ActiveMonitor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cbox = sender as ComboBox;
            if(cbox.SelectedIndex >= 0)
            {
                Position.Monitors.SetActiveScreen(cbox.SelectedIndex);
            }
        }

        private void EnableGamepadCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (loaded)
                Task.Run(() => Gamepad.Bindings.InitializeXInput());
        }

        private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            //e.Handled = true;

            if (UniversalPlatform.Startup.IsUniversalPlatform)
            {
                _ = UniversalPlatform.General.CheckForUpdates();
            }
            else
            {
                // Wix Installer
                //Update.UpdateHandler.CheckForUpdates(true);

                // For ClickOnce
                //InstallUpdateSyncWithInfo();

                // Squirrel
                SquirrelUpdate.UpdateButton();
            }
        }

        private void FeedbackButton_Click(object sender, RoutedEventArgs e)
        {
           // e.Handled = true;
            System.Diagnostics.Process.Start("https://discord.gg/HfDaFjp");
        }

        private void ResetLocalDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            //e.Handled = true;

            var result = OpenWindow.Notification($"You are about to reset Fuzion to default! This will delete all cached data and Fuzion will run as if you've installed it for the first time. Continue?", $"Reset Fuzion?", OpenWindow.NotificationWindowType.YesNo);

            if (result == OpenWindow.NotificationResult.Yes)
            {
                Cleanup.Clean.CustomData(Cleanup.Clean.DataStore.All);
            }
        }

        private void EdgeFade_Toggled(object sender, RoutedEventArgs e)
        {
            MainWindow.ToggleScrollViewerEdgeFade();
        }

        private void BackgroundVisuals_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (loaded)
                MainWindow.UpdateBackgroundVisuals();
        }

        private void IconSpacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (loaded)
                MainWindow.UpdateIconMargins();
        }

        private void ShowGameLabels_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow.GameTooltip.IsOpen = false;
        }

        private void SelectAllBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            BlacklistListview.SelectAll();
        }

        private void DeselectAllBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            BlacklistListview.SelectedItem = null;
        }

        private void RemoveFromBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            // Cache the selection
            var selectedItemsCached = new List<string>();

            for (int i = 0; i < BlacklistListview.SelectedItems.Count; i++)
            {
                selectedItemsCached.Add(BlacklistListview.SelectedItems[i].ToString());
            }

            // Deselect
            DeselectAllBlacklistButton_Click(null, null);

            // Remove
            for (int i = 0; i < selectedItemsCached.Count; i++)
            {
                string itemName = selectedItemsCached[i].ToString();
                bool removed = SettingsManager.Blacklist.Remove(itemName);

                if (removed)
                {
                    ObservableBlacklist.Remove(itemName);
                    Console.WriteLine("Removing from observable blacklist " + itemName);
                    //// set it again to make sure they're the same
                    //ObservableBlacklist = new System.Collections.ObjectModel.ObservableCollection<string>(SettingsManager.Blacklist.List.ToList());
                }
            }

            Console.WriteLine("ObservableBlacklist count " + ObservableBlacklist.Count);
            Console.WriteLine("Blacklist count " + SettingsManager.Blacklist.List.Count);
        }

        private void BlacklistListview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ToggleBlacklistRemoveButtonVisibility();
            //Console.WriteLine("Blacklist selection changed");
            //Console.WriteLine("Added items "+e.AddedItems);
            //Console.WriteLine("Selected count "+BlacklistListview.SelectedItems.Count);

            //for (int i = 0; i < BlacklistListview.SelectedItems.Count; i++)
            //{
            //    Console.WriteLine($"Item {i} name: {BlacklistListview.SelectedItems[i]}");
            //}
        }

        private void ToggleBlacklistRemoveButtonVisibility()
        {
            if (BlacklistListview.SelectedItems.Count == 0)
            {
                RemoveFromBlacklistButton.Visibility = Visibility.Hidden;
            }
            else
            {
                RemoveFromBlacklistButton.Visibility = Visibility.Visible;
            }
        }

        private void TestAddLeft_Click(object sender, RoutedEventArgs e)
        {

            MoveLeftPosTest(256d);
            //Application.Current.Dispatcher.Invoke(new Action(() =>
            //{
            //    NativeMethods.SetOnDesktop(AppWindow, true);
            //}));
        }

        private void TestSubLeft_Click(object sender, RoutedEventArgs e)
        {
            MoveLeftPosTest(-256d);
            //Application.Current.Dispatcher.Invoke(new Action(() =>
            //{
            //    NativeMethods.SetOnDesktop(AppWindow, false);
            //}));

        }

        private void TestResLeft_Click(object sender, RoutedEventArgs e)
        {
            MoveLeftPosTest(0d);
        }

        private void MoveTo0_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.MoveWindow(new WindowInteropHelper(AppWindow).Handle, 
                Position.Monitors.ActiveScreen.WorkingArea.Left, 0,
                (int)AppWindow.Width, (int)AppWindow.Height,
                true);
        }

        private void AutoScanForGamesCheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleAutoScanForGames();
        }

        private void SearchSteam_Toggled(object sender, RoutedEventArgs e)
        {
            AdjustVerticalSearchPosition();
        }

        private void DockTabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // StackPanelRow Height is 40, 120d is normal scroll wheel scroll delta
            double increment = 40d * (e.Delta / 120d);

            if(dockTabSVTarget > 0 || dockTabSVTarget < DockTabScrollViewer.ScrollableHeight)
                dockTabSVTarget -= increment;

            if(dockTabSVTarget < 0)
            {
                dockTabSVTarget = 0;
            }

            if(dockTabSVTarget > DockTabScrollViewer.ScrollableHeight)
            {
                dockTabSVTarget = DockTabScrollViewer.ScrollableHeight;
            }

            e.Handled = true;
        }

        double dockTabSVTarget = 0;
        bool dockTabSVCanLerp = true;
        public void DockTabSmoothScroll()
        {
            if(DockTabScrollViewer != null)
            {
                if(dockTabSVCanLerp)
                {
                    DockTabScrollViewer.ScrollToVerticalOffset(Extensions.MathExtensions.Lerp(DockTabScrollViewer.VerticalOffset, dockTabSVTarget, 0.01d));
                }
            }
        }

        private void DockTabScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // used to use thumb.dragstarted and dragcompleted events
            dockTabSVCanLerp = false;
        }

        private void DockTabScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            dockTabSVTarget = DockTabScrollViewer.VerticalOffset;
            dockTabSVCanLerp = true;
        }

        private void BackgroundAutoSize_Click(object sender, RoutedEventArgs e)
        {
            if (BackgroundAutoSizeCheckbox.IsChecked == true)
            {
                BackgroundWidthGrid.Visibility = Visibility.Collapsed;
                BackgroundHeightGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackgroundWidthGrid.Visibility = Visibility.Visible;
                BackgroundHeightGrid.Visibility = Visibility.Visible;
            }

            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
            MainWindow.UpdateBackgroundSize();
        }

        private void BackgroundSizeExample_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (loaded)
            {
                Settings.Default.Save();
                MainWindow.UpdateBackgroundSize();
            }
        }

        private void BackgroundEdgeToEdge_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();
            UniversalPlatform.OnUpdate.UpdateUWPSettings();
            MainWindow.UpdateBackgroundSize();
        }
    }
}
