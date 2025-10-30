using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fuzion.Native.IdleHook;
using Fuzion.Properties;
using Fuzion.LauncherSpecific;
using static Fuzion.MainWindow;

namespace Fuzion.LauncherSpecific
{
    static class ShadowLaunch
    {
        public static void UpdateState()
        {
            //if (Settings.Default.IsShadowLaunchEnabled)
            // Redundant call as this is checked later, needs improvement
            if (IdleTime.allShadowLaunchers.Any(b => b == true))
            {
                Enable();
            } else
            {
                Disable();
            }
        }

        private static void Enable()
        {
            if (LaunchedFromStartup)
            {
                IdleTime.Start();
            }
        }

        public static void Disable()
        {
            IdleTime.Stop();
        }
    }
}
