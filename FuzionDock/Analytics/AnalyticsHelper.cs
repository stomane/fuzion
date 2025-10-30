using Fuzion.Properties;
using Gappalytics.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Analytics
{
    public class AnalyticsHelper
    {
        private IAnalyticsSession _analyticsSession;

        private static AnalyticsHelper _Current;
        public static AnalyticsHelper Current
        {
            get
            {
                if (_Current == null)
                    Initialize();
                return _Current;
            }
        }

        public bool AnalyticsEnabled { get; set; }
        public bool IsTrial { get; set; }

        /// <summary>
        /// This method displays how to use non persistent mode of Gappalytics, each time you run will make a new unique user on GA dashboard
        /// For persistent mode, store first visit date, visit count (increment each time), and random number somewhere like app settings
        /// Use Constructor overload for Analytics sessions 
        /// </summary>
        public static void Initialize()
        {
            try
            {
                _Current = new AnalyticsHelper();

                // Initialize the user
                InitUser();

                // Add 1 to visit count
                Settings.Default.AnalyticsVisitCount++;

                // Replace UA code with your Google Analytics Tracking code
                _Current._analyticsSession = new AnalyticsSession("", "UA-178006201-1"
                    , Settings.Default.AnalyticsID
                    , Settings.Default.AnalyticsVisitCount
                    , DateTime.Parse(Settings.Default.AnalyticsDateTime, CultureInfo.InvariantCulture));

                Console.WriteLine("Parsed datetimenow is: "+ DateTime.Parse(Settings.Default.AnalyticsDateTime, CultureInfo.InvariantCulture));
                _Current.LogEvent("Fuzion Started", "Fuzion Event");
            }
            catch (Exception)
            {

            }
         
        }


        public void LogEvent(string url, string eventName)
        {
            if (string.IsNullOrEmpty(eventName) || string.IsNullOrEmpty(url))
            {
                eventName = "Null Name";
                url = "Null URL";
            }
                //throw new InvalidOperationException("Parameters cannot contain empty values");
            try
            {
                var request = _analyticsSession.CreatePageViewRequest(url, eventName);
                request.Send();
            }
            catch
            {
                // Keep silent exceptions for robust scenarios
            }
        }

        private static void InitUser()
        {
            StoreUserDataInSettings();

            //// User has no settings in registry
            //const string fuzionKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Fuzion";
            //try
            //{
            //    RegistryKey uninstallKey = Registry.CurrentUser.OpenSubKey(fuzionKeyPath, true);

            //    if (uninstallKey != null)
            //    {
            //        if (uninstallKey.GetValue("UniqueID") == null)
            //        {
            //            Random rnd = new Random();
            //            uninstallKey.SetValue("UniqueID", rnd.Next(1, 1000000));
            //            uninstallKey.SetValue("FirstDateTime", DateTime.Now.ToString(CultureInfo.InvariantCulture));
            //            uninstallKey.SetValue("LaunchCount", 0);
            //        } else
            //        {
            //            LoadUserDataFromRegistry();
            //        }
                  
            //    } else
            //    {
            //        StoreUserDataInSettings();
            //    }
            //}
            //catch (Exception)
            //{
            //    StoreUserDataInSettings();
            //}
        }

        private static void StoreUserDataInSettings()
        {
            // User has no settings
            if (Settings.Default.AnalyticsID == 0 && Settings.Default.AnalyticsDateTime.Length == 0 && Settings.Default.AnalyticsVisitCount == -1)
            {
                // create data
                Random rnd = new Random();
                Settings.Default.AnalyticsID = rnd.Next(1, 1000000);
                Settings.Default.AnalyticsDateTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                Settings.Default.AnalyticsVisitCount = 0;

                Settings.Default.Save();
            }
        }

        private static void LoadUserDataFromRegistry()
        {
            Console.WriteLine("Loading data from registry");
            const string fuzionKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Fuzion";
            try
            {
                RegistryKey uninstallKey = Registry.CurrentUser.OpenSubKey(fuzionKeyPath);

                Settings.Default.AnalyticsID = Convert.ToInt32(uninstallKey.GetValue("UniqueID"), CultureInfo.InvariantCulture);
                Settings.Default.AnalyticsDateTime = uninstallKey.GetValue("FirstDateTime").ToString();
                Settings.Default.AnalyticsVisitCount = Convert.ToInt32(uninstallKey.GetValue("LaunchCount"), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                StoreUserDataInSettings();
            }
        }
    }
}
