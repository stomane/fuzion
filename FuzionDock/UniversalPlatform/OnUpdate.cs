using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using Windows.Storage;
using System.Configuration;

namespace Fuzion.UniversalPlatform
{
    static class OnUpdate
    {
        public static void UpdateUWPSettings()
        {
            if (Startup.IsUniversalPlatform)
            {
                foreach (SettingsPropertyValue value in Properties.Settings.Default.PropertyValues)
                {
                    ApplicationData.Current.LocalSettings.Values[value.Name] = value.PropertyValue;
                }
            }
        }

        public static void LoadUWPSettings()
        {
            if (Startup.IsUniversalPlatform)
            {
                foreach (var setting in ApplicationData.Current.LocalSettings.Values)
                {
                    foreach (SettingsPropertyValue s in Properties.Settings.Default.PropertyValues)
                    {
                        if (s.Name == setting.Key)
                        {
                            s.PropertyValue = setting.Value;
                        }
                    }
                }

                Properties.Settings.Default.Save();
            }
        }
    }
}
