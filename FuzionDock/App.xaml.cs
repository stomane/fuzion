using Microsoft.Shell;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Fuzion.Programs;
using Fuzion.Icons;
using Sentry;

namespace Fuzion
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        // TODO: Make this unique! Controls single instance of application
        private const string Unique = "FuzionDokkByTonyVlahov";

        [STAThread]
        public static void Main()
        {
#if DEBUG
            Debug.DebugFileLog.Initialize();
#endif
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                SentrySdk.Init(options =>
                {
                    options.Dsn = Constants.SentryDsn;

                    // Usage tracking only for now: Release Health sessions give us "how many
                    // people have the app open" without capturing any error/crash data. Actual
                    // crash reporting is deliberately left off until it's exposed as an
                    // explicit user opt-in setting - Init() captures unhandled exceptions by
                    // default, so that's disabled here.
                    options.AutoSessionTracking = true;
                    options.DisableAppDomainUnhandledExceptionCapture();

                    options.Debug = false;
                });

                var application = new App();
                application.InitializeComponent();
                application.Run();

                // Run() returns once the message loop exits, regardless of which shutdown
                // path triggered it - flush the Release Health session and any queued events
                // here so every exit path is covered in one place.
                SentrySdk.Close();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
            else
            {
                // Fuzion already running, focus it
                TrayIcon.FocusFuzionOnClick();
            }
        }

        #region ISingleInstanceApp Members
        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // Bring window to foreground if minimized
            if (MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.WindowState = WindowState.Normal;
            }

            MainWindow.Activate();

            return true;
        }
        #endregion

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length == 1)
            {
                //System.Windows.Forms.MessageBox.Show("Arguments are: " + e.Args[0]);


                if (e.Args[0] == "-startup")
                {
                    Fuzion.MainWindow.LaunchedFromStartup = true;

                }

                if (e.Args[0] == "-console")
                {
                    Debug.DebugConsole.OpenDebugWindow();
                }
            }


            // can set it directly - win32 safe
            if(UniversalPlatform.Startup.RanFromStartup())
                Fuzion.MainWindow.LaunchedFromStartup = true;

            MainWindow mw = new MainWindow();
            Fuzion.MainWindow.AppWindow = mw;
            mw.Show();
            
        }
    }
}
