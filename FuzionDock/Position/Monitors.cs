using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fuzion.Properties;

namespace Fuzion.Position
{
    public static class Monitors
    {
        public static Screen ActiveScreen { get; private set; } = GetActiveScreen();
        public static int ScreenCount { get; } = Screen.AllScreens.Length;
        public static List<int> ScreenIndexes { get; private set; } = GetScreenIndexes();

        static Monitors()
        {
            ConsoleWriteActiveScreenStats();
        }

        static void ConsoleWriteActiveScreenStats()
        {
            Console.WriteLine("Active screen index " + Settings.Default.ActiveScreenIndex + " has width of " + ActiveScreen.Bounds.Width + " and height of " + ActiveScreen.Bounds.Height);
            Console.WriteLine("WorkingArea");
            Console.WriteLine("Left: " + ActiveScreen.WorkingArea.Left);
            Console.WriteLine("Right: " + ActiveScreen.WorkingArea.Right);
            Console.WriteLine("Top: " + ActiveScreen.WorkingArea.Top);
            Console.WriteLine("Bottom: " + ActiveScreen.WorkingArea.Bottom);
            Console.WriteLine("X: " + ActiveScreen.WorkingArea.X);
            Console.WriteLine("Y: " + ActiveScreen.WorkingArea.Y);
            Console.WriteLine("Bounds");
            Console.WriteLine("Left: " + ActiveScreen.Bounds.Left);
            Console.WriteLine("Right: " + ActiveScreen.Bounds.Right);
            Console.WriteLine("Top: " + ActiveScreen.Bounds.Top);
            Console.WriteLine("Bottom: " + ActiveScreen.Bounds.Bottom);
            Console.WriteLine("Size: " + ActiveScreen.Bounds.Size);
            Console.WriteLine("Window State: " + MainWindow.AppWindow.WindowState);
            Console.WriteLine("Virtual Screen Left: "+ System.Windows.SystemParameters.VirtualScreenLeft);

        }

        static Screen GetActiveScreen()
        {
            if(Settings.Default.ActiveScreenIndex >= Screen.AllScreens.Length)
            {
                Settings.Default.ActiveScreenIndex = 0;
                return Screen.AllScreens[0];
            }

            return Screen.AllScreens[Settings.Default.ActiveScreenIndex];
        }

        public static void SetActiveScreen(int index)
        {
            ActiveScreen = Screen.AllScreens[index];
            ConsoleWriteActiveScreenStats();
            MainWindow.CenterWindowOnScreen(System.Reflection.MethodBase.GetCurrentMethod().Name);
        }

        public static void UpdateScreenIndexes()
        {
            ScreenIndexes = GetScreenIndexes();

            if(SettingsWindow.Instance != null && SettingsWindow.Instance.ActiveMonitorItemSource != null)
            {
                //Clear manually without removing first index
                for (int i = 1; i < SettingsWindow.Instance.ActiveMonitorItemSource.Count; i++)
                {
                    SettingsWindow.Instance.ActiveMonitorItemSource.Remove(i);
                }

                for (int i = 1; i < ScreenIndexes.Count; i++)
                {
                    SettingsWindow.Instance.ActiveMonitorItemSource.Add(ScreenIndexes[i]); //could just use i
                }
            }
        
            // If monitor is not there anymore, revert to screen 0
            if (!Screen.AllScreens.Contains(ActiveScreen))
            {
                Settings.Default.ActiveScreenIndex = 0;
                SetActiveScreen(0);
            }
        }

        static List<int> GetScreenIndexes()
        {
            var result = new List<int>();

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                //Console.WriteLine("Screen at index "+i + " device name is "+screens[i].DeviceName);
                result.Add(i);
            }

            return result;
        }
    }
}
