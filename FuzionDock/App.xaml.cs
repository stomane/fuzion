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
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();

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
