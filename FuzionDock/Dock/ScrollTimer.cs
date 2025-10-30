using System;
using System.Windows.Threading;

namespace Fuzion.Dock
{
    /// <summary>
    /// Static DispatcherTimer which runs every tick, used for smooth scrolling various ScrollViewers
    /// but not the main one
    /// </summary>
    internal static class ScrollTimer
    {
        private static DispatcherTimer Timer;

        public static void Start()
        {
            Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromTicks(1)
            };

            Timer.Tick += Timer_Tick;
            Timer.Start();
        }

        private static void Timer_Tick(object sender, EventArgs e)
        {
            // Point to smooth scrolling
            if(SettingsWindow.Instance != null)
            {
                SettingsWindow.Instance.DockTabSmoothScroll();
            }
           
        }
    }
}
