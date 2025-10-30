using Fuzion.WindowsManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.ApplicationModel;

namespace Fuzion.UniversalPlatform
{
    static class General
    {
        public static async Task CheckForUpdates()
        {
            if (Startup.IsUniversalPlatform)
            {
                var result = await Package.Current.CheckUpdateAvailabilityAsync();
                if (result.Availability == PackageUpdateAvailability.Available)
                {
                    OpenWindow.Notification("There's a new update! Restart your app to install it", "Update Checker");
                }

                if (result.Availability == PackageUpdateAvailability.NoUpdates)
                {
                    OpenWindow.Notification("You're already up to date", "Update checker");
                }

                if (result.Availability == PackageUpdateAvailability.Required)
                {
                    OpenWindow.Notification("Oh dear, this update is a must! So many squished bugs", "Update checker");
                }

                if(result.Availability == PackageUpdateAvailability.Error || result.Availability == PackageUpdateAvailability.Unknown)
                {
                    OpenWindow.Notification("Something went wrong, try again later", "Update checker");
                }
            }

            //if(result.Availability == PackageUpdateAvailability.NoUpdates)
            //{
            //    OpenWindow.Notification("You're up to date");
            //}
        }
    }
}
