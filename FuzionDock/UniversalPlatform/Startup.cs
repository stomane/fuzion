using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Popups;
using Windows.ApplicationModel.Activation;
using DesktopBridge;
using Fuzion.WindowsManager;

namespace Fuzion.UniversalPlatform
{
    static class Startup
    {
        public static bool IsUniversalPlatform { get; private set; } = CheckIsUWPMode();
        // set in checkstartupstate
        public static StartupTaskState CurrentStartupState;

        public static async Task<bool> GetCurrentStartupState()
        {
            if (IsUniversalPlatform)
            {
                StartupTask startupTask = await StartupTask.GetAsync("FuzionStartupTask");
                CurrentStartupState = startupTask.State;

                if (CurrentStartupState == StartupTaskState.Enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckIsUWPMode()
        {
            Helpers helpers = new Helpers();
            return helpers.IsRunningAsUwp();
        }

        public static async void UpdateStartupState(bool enable)
        {
            if (IsUniversalPlatform)
            {
                StartupTask startupTask = await StartupTask.GetAsync("FuzionStartupTask");
                //System.Windows.Forms.MessageBox.Show("Startup task state: " + startupTask.State.ToString());
                Console.WriteLine("Startup task state: " + startupTask.State.ToString());
                switch (startupTask.State)
                {
                    case StartupTaskState.Disabled:
                        if (enable)
                        {
                            StartupTaskState state = await startupTask.RequestEnableAsync();
                            //System.Windows.Forms.MessageBox.Show("Startup State is " + startupTask.State.ToString(), startupTask.State.ToString());
                        }
                        // Task is disabled but can be enabled.
                        break;
                    case StartupTaskState.DisabledByUser:
                        OpenWindow.Notification("Unable to toggle setting because Startup State is " + startupTask.State.ToString() + ". You can toggle the setting from Task Manager >>> Startup", startupTask.State.ToString());
                        //Console.WriteLine("Startup state: " + startupTask.State.ToString());
                        break;
                    case StartupTaskState.DisabledByPolicy:
                        //    "Startup disabled by group policy, or not supported on this device");
                        break;
                    case StartupTaskState.Enabled:
                        if (!enable)
                        {
                            startupTask.Disable();
                        }
                        break;
                }
            }
        }

        public static bool RanFromStartup()
        {
            //System.Windows.Forms.MessageBox.Show("Attempting to get activated event args");
            try
            {
                IActivatedEventArgs args = AppInstance.GetActivatedEventArgs();
                if (args.Kind == ActivationKind.StartupTask)
                    return true;
            }
            catch (Exception)
            {

            }

            return false;
        }
    }
}
