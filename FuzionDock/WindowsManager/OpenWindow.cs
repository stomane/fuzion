using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
                // x center, y center
                var gamePos = RightClickedGame.PointToScreen(new Point(Properties.Settings.Default.StartupIconSize / 2d, Properties.Settings.Default.StartupIconSize / 2d));

                switch (Properties.Settings.Default.DockLocation)
                {
                    case 0:
                        startupWindowPos = new Point(gamePos.X - 390d / 2d, gamePos.Y + Properties.Settings.Default.StartupIconSize / 2d + 10d);
                        break;
                    case 1:
                        startupWindowPos = new Point(gamePos.X - 390d / 2d, gamePos.Y - Properties.Settings.Default.StartupIconSize / 2d - 190d);
                        break;
                    case 2:
                        startupWindowPos = new Point(gamePos.X + Properties.Settings.Default.StartupIconSize / 2d + 10d, gamePos.Y - 180d / 2d);
                        break;
                    case 3:
                        startupWindowPos = new Point(gamePos.X - Properties.Settings.Default.StartupIconSize / 2d - 400d, gamePos.Y - 180d / 2d);
                        break;
                    default:
                        startupWindowPos = new Point(gamePos.X - 390d / 2d, gamePos.Y + 10d);
                        break;
                }

                //editGameWindow.Top = Native.NativeMethods.GetMousePosPinvoke().Y - 10d;
                //editGameWindow.Left = Native.NativeMethods.GetMousePosPinvoke().X - 10d;

                editGameWindow.Left = startupWindowPos.X;
                editGameWindow.Top = startupWindowPos.Y;
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
