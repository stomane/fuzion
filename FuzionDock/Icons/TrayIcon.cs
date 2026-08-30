using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using Fuzion.Native;
using Fuzion.Properties;
using Fuzion.SettingsManager;
//using SharpDX.DirectInput;
using static Fuzion.MainWindow;

namespace Fuzion.Icons
{
    class TrayIcon
    {
        public static NotifyIcon notifyIcon = new NotifyIcon();

        // For styling check WPF wrapper for notifyicon

        public TrayIcon()
        {
            RefreshTrayArea();

            Stream stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/trayIconColor.ico")).Stream;
            notifyIcon.Icon = new Icon(stream);
            notifyIcon.Text = UiText.AppName;
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;

            ContextMenu nMenu = new ContextMenu();
            //notifyIcon.Click += NotifyIcon_FocusClick;
            notifyIcon.MouseClick += NotifyIcon_LeftMouseClick;

            MenuItem exit_menuItem = new MenuItem
            {
                Text = UiText.MenuExit
            };
            exit_menuItem.Click += Exit_menuItem_Click;

            MenuItem settings_menuItem = new MenuItem
            {
                Text = UiText.MenuSettings
            };
            settings_menuItem.Click += Settings_menuItem_Click;

            MenuItem rescan_menuItem = new MenuItem
            {
                Text = UiText.MenuRescan
            };
            rescan_menuItem.Click += Rescan_menuItem_Click;

            MenuItem add_menuItem = new MenuItem
            {
                Text = UiText.MenuAdd
            };
            add_menuItem.Click += Add_menuItem_Click;

            nMenu.MenuItems.AddRange(new MenuItem[]
            {
                add_menuItem,
                rescan_menuItem,
                settings_menuItem,
                exit_menuItem
            });

            notifyIcon.ContextMenu = nMenu;
            notifyIcon.Visible = true;

            notifyIcon.Tag = "fuzionnotifyicon";

            stream.Close();
            //nMenu.Dispose();
        }

        private void NotifyIcon_LeftMouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                FocusFuzionOnClick(false);
            }

        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            //Console.WriteLine("Clicking on tray icon");
        }

        private void NotifyIcon_FocusClick(object sender, EventArgs e)
        {
            // enable noForce because we're the main thread
            //FocusFuzionOnClick(true);
        }

        public static void FocusFuzionOnClick(bool forceForeground = true)
        {
            // Probably should remove the option to not stick to desktop, or hide it better
            if (Settings.Default.StickToDesktop)
            {
                NativeMethods.ShowDesktop();
            }

            if (forceForeground == true)
            {
                NativeMethods.ForceSetForegroundWindow(AppWindow);
            }

            // Activate mainwindow
            AppWindow.Activate();

            // Highlight a game
            HighlightGameOnFocus();
        }

        private void Exit_menuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.Save();
            notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        private void Settings_menuItem_Click(object sender, EventArgs e)
        {
            WindowsManager.OpenWindow.Instance.Settings();
        }

        private void Rescan_menuItem_Click(object sender, EventArgs e)
        {
            RescanAsync();
            AppWindow.Activate();
        }

        private void Add_menuItem_Click(object sender, EventArgs e)
        {
            AppWindow.AddGameManually();
            Console.WriteLine("Add Game" + sender);
        }

        #region "Refresh Notification Area Icons"

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);


        public static void RefreshTrayArea()
        {
            IntPtr systemTrayContainerHandle = FindWindow("Shell_TrayWnd", null);
            IntPtr systemTrayHandle = FindWindowEx(systemTrayContainerHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            IntPtr sysPagerHandle = FindWindowEx(systemTrayHandle, IntPtr.Zero, "SysPager", null);
            IntPtr notificationAreaHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "Notification Area");
            if (notificationAreaHandle == IntPtr.Zero)
            {
                notificationAreaHandle = FindWindowEx(sysPagerHandle, IntPtr.Zero, "ToolbarWindow32", "User Promoted Notification Area");
                IntPtr notifyIconOverflowWindowHandle = FindWindow("NotifyIconOverflowWindow", null);
                IntPtr overflowNotificationAreaHandle = FindWindowEx(notifyIconOverflowWindowHandle, IntPtr.Zero, "ToolbarWindow32", "Overflow Notification Area");
                RefreshTrayArea(overflowNotificationAreaHandle);
            }
            RefreshTrayArea(notificationAreaHandle);
        }


        private static void RefreshTrayArea(IntPtr windowHandle)
        {
            const uint wmMousemove = 0x0200;
            RECT rect;
            GetClientRect(windowHandle, out rect);
            for (var x = 0; x < rect.right; x += 5)
                for (var y = 0; y < rect.bottom; y += 5)
                    SendMessage(windowHandle, wmMousemove, 0, (y << 16) + x);
        }
        #endregion
    }
}
