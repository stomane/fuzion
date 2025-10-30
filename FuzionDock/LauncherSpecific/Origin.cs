using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using static Fuzion.Native.NativeMethods;

namespace Fuzion.LauncherSpecific
{
    static class Origin
    {
        public static bool Exists { get; private set; } = OriginExists();
        public static bool ShadowLaunchEnabled { get; set; }
        public static string Path { get; private set; }
        public static string WorkDir { get; private set; }
        public static string Arguments { get; } = "/StartClientMinimized"; // Crashes origin, weird, also -AutoStart seems to be used on startup
        private static DispatcherTimer CloseDispatcher { get; set; } = CloseDispatcherInit();

        public const string ExeName = "Origin.exe";
        public const string ClientProcessName = "Origin";
        public const string RegistryLocation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Origin";
        private static int CloseTickIndex;

        private static bool OriginExists()
        {
            // new method which uses 32bit registry
            try
            {
                // Opens the registry in 32bit mode since in 64bits battle.net uninstall entry is under Wow6432Node Key
                using (RegistryKey registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    // goes to the uninstall entry on the battle.net client and retrieves the InstallLocation key to get the path
                    using (RegistryKey originKey = registry.OpenSubKey(RegistryLocation))
                    {
                        if(originKey != null)
                        {
                            Path = System.IO.Path.Combine(originKey.GetValue("InstallLocation").ToString(), ExeName);
                            WorkDir = originKey.GetValue("InstallLocation").ToString();
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static DispatcherTimer CloseDispatcherInit()
        {
            DispatcherTimer disp = new DispatcherTimer();
            {
                disp.Interval = TimeSpan.FromSeconds(3d);
            }

            disp.Tick += CloseDisp_Tick;

            return disp;
        }

        public static bool IsRunning()
        {
            Process[] processList = Process.GetProcessesByName("Origin");

            if (processList.Length != 0)
            {
                return true;
            }

            return false;
        }

        private static void CloseDisp_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("Origin dispatcher tick");

            Process[] processList = Process.GetProcessesByName("Origin");

            if (processList.Length != 0 && processList[0] != null)
            {
                Console.WriteLine("Origin processes count = " + processList.Length);
                //SetForegroundWindow(processList[0].MainWindowHandle);
                Console.WriteLine("Attempting to close Origin");
                //Process p = new Process();
                //p.StartInfo.FileName = Fuzion.MainWindow.DefaultAssetPath + @"\msghandler\SendMessage.exe";
                //p.StartInfo.Arguments = @"/message:17 /windowhandle:" + processList[0].MainWindowHandle; //message:17 is queryendsession
                //p.Start();
                //p.Dispose();
                Fuzion.Icons.TrayIcon.SendMessage(processList[0].MainWindowHandle, 17, 0, 0);
            } else
            {
                CloseDispatcher.Stop();
            }

            CloseTickIndex++;

            if(CloseTickIndex >= 10)
            {
                CloseDispatcher.Stop();
            }
        }

        public static void Close()
        {
            CloseDispatcher.Start();
        }

        public static void MinimizeToTray()
        {
            Process[] processList = Process.GetProcessesByName(ClientProcessName);

            if(processList.Length != 0)
            {
                processList[0].CloseMainWindow();
            }
        }

        public static void Kill()
        {
            Process[] processList = Process.GetProcessesByName("Origin");

            foreach (var item in processList)
            {
                Console.WriteLine("Process for origin found: " + item.ProcessName);
            }
            if (processList.Length != 0)
            {
                processList[0].Kill();
                Icons.TrayIcon.RefreshTrayArea();
            }
        }
    }
}
