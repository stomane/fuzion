using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Reflection;
using Fuzion.WindowsManager;

namespace Fuzion.UniversalPlatform
{
    static class Startup
    {
        /// <summary>
        /// True when running from the MSIX package (the Store build), false when running the
        /// loose build straight out of bin. Both are the same full-trust Win32 process - this
        /// only tells the app which distribution it was launched from, so it can skip things
        /// the package owns (for example, the Store handles updates).
        /// </summary>
        public static bool IsUniversalPlatform { get; } = DetectPackagedContext();

        static bool DetectPackagedContext()
        {
            try
            {
                return new global::DesktopBridge.Helpers().IsRunningAsUwp();
            }
            catch (Exception ex)
            {
                // Nothing here should ever stop the app starting up
                Console.WriteLine("Failed to detect packaged context, assuming unpackaged: " + ex.Message);
                return false;
            }
        }


        public static Task<bool> GetCurrentStartupState()
        {
            return Task.FromResult(IsRunOnStartupEnabled());
        }

        private static bool IsRunOnStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                {
                    return key?.GetValue("FuzionDock") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void UpdateStartupState(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enable)
                    {
                        key.SetValue("FuzionDock", Assembly.GetExecutingAssembly().Location);
                    }
                    else
                    {
                        key.DeleteValue("FuzionDock", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to update startup state: " + ex.Message);
            }
        }

        public static bool RanFromStartup()
        {
           // Standard win32 apps effectively always run "normally". 
           // If we needed to check headers or args, we could. 
           // For now, returning false usually simulates "normal launch".
           // Original code checked for ActivationKind.StartupTask.
           return false; 
        }
    }
}
