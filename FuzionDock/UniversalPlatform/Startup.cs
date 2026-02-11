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
        public static bool IsUniversalPlatform { get; private set; } = false; // Hardcoded to false
        
        // Emulate the enum for compatibility with existing code if needed, or just use bools/strings in calling code.
        // For now, let's keep the enum usage in mind or mock it if strictly typed elsewhere.
        // Looking at usage: 
        // MainWindow.xaml.cs: Settings.Default.LaunchOnStartup = await UniversalPlatform.Startup.GetCurrentStartupState().ConfigureAwait(false);
        // Settings.Default.LaunchOnStartup is likely a boolean? checking usage...
        // Actually MainWindow.xaml.cs line 476 implies it returns a boolean? 
        // "Settings.Default.LaunchOnStartup = await UniversalPlatform.Startup.GetCurrentStartupState().ConfigureAwait(false);"
        // Wait, GetCurrentStartupState returned Task<bool> in the original file.
        
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
