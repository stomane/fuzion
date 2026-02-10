using Fuzion.WindowsManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Fuzion.UniversalPlatform
{
    static class General
    {
        public static async Task CheckForUpdates()
        {
            if (Startup.IsUniversalPlatform)
            {
               // Update logic removed as we are running in standalone mode.
               await Task.CompletedTask;
            }
        }
    }
}
