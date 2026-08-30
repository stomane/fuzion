using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Fuzion.MainWindow;

namespace Fuzion.WindowsManager
{
    public class OpenWindow
    {
        public static OpenWindow Instance { get; private set; }
        static OpenWindow()
        {
            Instance = new OpenWindow();
        }

        public void Settings()
        {
            //Properties.Settings.Default.IsGridLocked = true;

            if (!OpenWindowsManager.IsWindowOpen("Settings"))
            {
                //OpenWindowsManager.Tags.Add("Settings");
                SettingsWindow settingsWindow = new SettingsWindow();
                OpenWindowsManager.WindowReferenceControl("Settings", settingsWindow, OpenWindowsManager.R.Add);
                settingsWindow.ChangeIconsSizeEvent += SettingsWindow_ChangeIconsSizeEvent;
                settingsWindow.SetDockLocationEvent += SettingsWindow_SetDockLocationEvent;
                settingsWindow.Show();
            }
            else
            {
                int index = OpenWindowsManager.Tags.IndexOf("Settings");
                SettingsWindow focusThis = OpenWindowsManager.Windows[index] as SettingsWindow;
                focusThis.Focus();
            }
        }

        private void SettingsWindow_ChangeIconsSizeEvent(object sender, IconSizeChangedEventArgs e)
        {
            AppWindow.UpdateGameIconSizes();
        }

        private void SettingsWindow_SetDockLocationEvent(object sender, DockLocationEventArgs e)
        {
            SetDockLocation();
            CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

        // Editgame window shrunk 180h, expanded 240h - in App.xaml - OnlineIconsShrinkBorder storyboard
        // Width = 390
        public void EditGame()
        {
            //Properties.Settings.Default.IsGridLocked = true;

            string newWindowTag = $"{RightClickedGame.DisplayName} Card";

            if (!OpenWindowsManager.IsWindowOpen(newWindowTag))
            {
                EditGameWindow editGameWindow = new EditGameWindow();
                OpenWindowsManager.WindowReferenceControl(newWindowTag, editGameWindow, OpenWindowsManager.R.Add);
                editGameWindow.Tag = newWindowTag;
                editGameWindow.SendEditedInfoEvent += EditGameWindow_GameInfoEditedEvent;

                Point startupWindowPos;
                double cardWidth = editGameWindow.Width;
                double cardHeight = editGameWindow.Height;
                const double gap = 10d;

                // The dock's icon grid is rendered through a scale transform (see
                // Dock.Scrolling's screenMultiplier), so RightClickedGame.ActualWidth/Height
                // are in the icon's local, pre-scale units - not the AppWindow-space units that
                // gamePos/Window.Left/Top use. Measure the icon's top-left and center through
                // the same ancestor transform so the resulting half-width/half-height are
                // already in the correct (scaled) units, instead of reusing the raw local
                // values as offsets.
                GeneralTransform toAppWindow = RightClickedGame.TransformToAncestor(MainWindow.AppWindow);
                Point iconTopLeftInAppWindow = toAppWindow.Transform(new Point(0, 0));
                Point iconCenterInAppWindow = toAppWindow.Transform(new Point(RightClickedGame.ActualWidth / 2d, RightClickedGame.ActualHeight / 2d));

                double iconHalfWidth = iconCenterInAppWindow.X - iconTopLeftInAppWindow.X;
                double iconHalfHeight = iconCenterInAppWindow.Y - iconTopLeftInAppWindow.Y;

                var gamePos = new Point(MainWindow.AppWindow.Left + iconCenterInAppWindow.X, MainWindow.AppWindow.Top + iconCenterInAppWindow.Y);

                System.Diagnostics.Debug.WriteLine(
                    $"[EditCardPos] DockLocation={Properties.Settings.Default.DockLocation} " +
                    $"AppWindow.Left={MainWindow.AppWindow.Left} AppWindow.Top={MainWindow.AppWindow.Top} " +
                    $"RightClickedGame.ActualWidth={RightClickedGame.ActualWidth} RightClickedGame.ActualHeight={RightClickedGame.ActualHeight} " +
                    $"iconTopLeftInAppWindow={iconTopLeftInAppWindow} iconCenterInAppWindow={iconCenterInAppWindow} " +
                    $"iconHalfWidth={iconHalfWidth} iconHalfHeight={iconHalfHeight} " +
                    $"cardWidth={cardWidth} cardHeight={cardHeight} gamePos={gamePos}");

                switch (Properties.Settings.Default.DockLocation)
                {
                    case 0: // top dock -> card appears below the icon, horizontally centered
                        startupWindowPos = new Point(gamePos.X - cardWidth / 2d, gamePos.Y + iconHalfHeight + gap);
                        break;
                    case 1: // bottom dock -> card appears above the icon, horizontally centered
                        startupWindowPos = new Point(gamePos.X - cardWidth / 2d, gamePos.Y - iconHalfHeight - gap - cardHeight);
                        break;
                    case 2: // left dock -> card appears to the right of the icon, vertically centered
                        startupWindowPos = new Point(gamePos.X + iconHalfWidth + gap, gamePos.Y - cardHeight / 2d);
                        break;
                    case 3: // right dock -> card appears to the left of the icon, vertically centered
                        startupWindowPos = new Point(gamePos.X - iconHalfWidth - gap - cardWidth, gamePos.Y - cardHeight / 2d);
                        break;
                    default:
                        startupWindowPos = new Point(gamePos.X - cardWidth / 2d, gamePos.Y + gap);
                        break;
                }

                // Keep the card fully within the active monitor's working area (accounts for
                // taskbar and per-monitor DPI, same helper the dock itself uses) so it never
                // spawns partly off-screen near a monitor edge.
                Rect workingArea = MainWindow.GetActiveScreenWorkingAreaDip();
                editGameWindow.Left = ClampToRange(startupWindowPos.X, workingArea.Left, workingArea.Right - cardWidth);
                editGameWindow.Top = ClampToRange(startupWindowPos.Y, workingArea.Top, workingArea.Bottom - cardHeight);

                System.Diagnostics.Debug.WriteLine(
                    $"[EditCardPos] startupWindowPos={startupWindowPos} workingArea={workingArea} " +
                    $"final Left={editGameWindow.Left} Top={editGameWindow.Top}");

                editGameWindow.Show();
                editGameWindow.EditCurrentGame(RightClickedGame);
            }
            else
            {
                int index = OpenWindowsManager.Tags.IndexOf(newWindowTag);
                EditGameWindow focusThis = OpenWindowsManager.Windows[index] as EditGameWindow;
                focusThis.AnimateWindowHighlight();
                focusThis.Focus();
            }
        }

        // Clamps a proposed window coordinate into [min, max]; if the working area is smaller
        // than the window itself (max < min), just pin to the start of the area.
        private static double ClampToRange(double value, double min, double max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Max(min, Math.Min(value, max));
        }

        private void EditGameWindow_GameInfoEditedEvent(object sender, ChangeGameInfoEventArgs e)
        {
            if (e.IsUserModified)
            {
                RightClickedGame.Path = e.Path;
                RightClickedGame.Arguments = e.Arguments;
                RightClickedGame.PathType = e.PathType;
                RightClickedGame.IsUserModified = e.IsUserModified;
                RightClickedGame.DockName = e.DockName;
                RightClickedGame.Launcher = e.Launcher;
            }
            else
            {
                RightClickedGame.Path = RightClickedGame.OriginalPath;
                RightClickedGame.Arguments = RightClickedGame.OriginalArguments;
                RightClickedGame.PathType = RightClickedGame.OriginalPathType;
                RightClickedGame.IsUserModified = false;
                RightClickedGame.DockName = e.DockName;
                RightClickedGame.Launcher = RightClickedGame.OriginalLauncher;
            }

            //UpdateGameTooltip(RightClickedGame);

            SettingsManager.GeneralSettings.UpdateSettings();
        }

        public enum NotificationWindowType { Ok, OkCancel, YesNo, RemoveGame }
        public enum NotificationResult { No, Yes, YesBlacklist }

        public static void Notification(string text)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                var nWindow = new FuzionNotificationWindow(text, "Hmm...");
                nWindow.ShowDialog();
            });
        }

        public static void Notification(string text, string title)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                var nWindow = new FuzionNotificationWindow(text, title);
                nWindow.ShowDialog();
            });
        }

        public static NotificationResult Notification(string text, string title, NotificationWindowType nwt)
        {
            var nWindow = new FuzionNotificationWindow(text, title, nwt);
            nWindow.ShowDialog();
            return nWindow.NotificationResultStatus;
        }

        /// <summary>
        /// Show a customizable Notification toast
        /// </summary>
        /// <param name="text"></param>
        /// <param name="title"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="imgSrc">"Example: "pack://application:,,,/Assets/trayIconColor.ico"</param>
        public static void NotificationToast(string text, string title, double width = 210d, double height = 100d, string imgSrc = "", double hideInterval = 3d)
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                System.Windows.Media.Imaging.BitmapImage img = new System.Windows.Media.Imaging.BitmapImage();
                if (imgSrc.Length > 0)
                {
                    img.BeginInit();
                    img.UriSource = new Uri(imgSrc);
                    img.EndInit();
                }
                //System.Windows.Media.ImageSource imgSrcToPass;
                //imgSrcToPass = img;

                var nWindow = new FuzionToastWindow(text, title, width, height, img, hideInterval);
                //Point startupWindowPos;
                //switch (Properties.Settings.Default.DockLocation)
                //{
                //    case 0: //top
                //        nWindow.Left = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).X - width/2d;
                //        nWindow.Top = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).Y;
                //        break;
                //    case 1: //bottom
                //        nWindow.Left = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).X + width/2d;
                //        nWindow.Top = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).Y + height;
                //        break;
                //    case 2: //left
                //        nWindow.Left = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).X;
                //        nWindow.Top = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).Y - height/2d;
                //        break;
                //    case 3: //right
                //        nWindow.Left = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).X - width;
                //        nWindow.Top = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).Y - height/2d;
                //        break;
                //    default:
                //        nWindow.Left = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).X;
                //        nWindow.Top = AppWindow.SearchBarParent.PointToScreen(new Point(0, 0)).Y;
                //        break;
                //}

                nWindow.Left = SystemParameters.WorkArea.Right - width - 5d;
                nWindow.Top = SystemParameters.WorkArea.Bottom - height - 5d;

                nWindow.Width = width;
                nWindow.Height = height;

                nWindow.ShowActivated = false;
                nWindow.Show();
            });
        }
    }
}
