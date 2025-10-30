using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static Fuzion.MainWindow;
using static Fuzion.LauncherSpecific.BattleNet;
using System.Windows;
using static Fuzion.Native.NativeMethods;
using static Fuzion.SettingsManager.GeneralSettings;
using Windows.System;
using Fuzion.Extensions;

namespace Fuzion.Programs
{
    class Launch
    {
        // CMD method
        //string cmdString;
        //cmdString = "/c start /b \"\" \"" + game.Path + "\" " + game.Arguments; //&start {System.IO.Path.GetFileName(game.Path)} {game.WorkDir} & is newline
        //Console.WriteLine(cmdString);
        //Process.Start("CMD", cmdString);

            // This needs to support multiple origin game launching
            // Needs refactoring
        public static Game originGame;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Process Watcher disposes of itself when finished")]
        public static void LaunchGame(Game game, bool closeOnExit = false)
        {
            Properties.Settings.Default.IsGridLocked = true;

            try
            {
                if(game.Launcher != BelongsToLauncher.Origin)
                {
                    GameProcess(game).Start();

                    if (closeOnExit)
                    {
                        //NO need to hook to: Battle.net, UWP, GOG
                        if (game.Launcher != BelongsToLauncher.BattleNet
                            && game.Launcher != BelongsToLauncher.UWP
                            && game.Launcher != BelongsToLauncher.GOG)
                        {
                            Processes.ProcessWatcher pWatcher = new Processes.ProcessWatcher();
                            pWatcher.StartWatching(game);
                        }
                    }
                } else
                {
                    GameProcess(game).Start();
                }

                // Needs to be redone before it fully works
                //if (game.Launcher == BelongsToLauncher.Origin)
                //{
                //    //Animate game double click
                //    game.BeginStoryboard(game.LaunchStoryboard);
                //    if (closeOnExit)
                //    {
                //        originGame = game;
                //        if (!LauncherSpecific.Origin.IsRunning())
                //        {
                //            // Start Origin first so we can close it later
                //            Process gProcess = new Process();
                //            gProcess.StartInfo.FileName = LauncherSpecific.Origin.Path;
                //            gProcess.StartInfo.WorkingDirectory = LauncherSpecific.Origin.WorkDir;
                //            gProcess.Start();

                //            Console.WriteLine("Starting process: " + gProcess.ProcessName);

                //            DispatcherTimer originDispatcher = new DispatcherTimer();
                //            originDispatcher.Interval = TimeSpan.FromSeconds(10d);
                //            originDispatcher.Tick += OriginDispatcher_Tick;
                //            originDispatcher.Start();
                //        }
                //        else
                //        {
                //            DispatcherTimer originDispatcher = new DispatcherTimer();
                //            originDispatcher.Interval = TimeSpan.FromSeconds(0d);
                //            originDispatcher.Tick += OriginDispatcher_Tick;
                //            originDispatcher.Start();
                //        }
                //    } else
                //    {
                //        Process gProcess = new Process();
                //        gProcess.StartInfo.FileName = originGame.Path;
                //        gProcess.StartInfo.Arguments = originGame.Arguments;
                //        gProcess.StartInfo.WorkingDirectory = originGame.WorkDir;
                //        gProcess.Start();
                //        Console.WriteLine("Starting process: " + gProcess?.ProcessName);
                //    }
                //}
            }
            catch (Exception ex)
            {
                WindowsManager.OpenWindow.Notification("Failed to launch game: "+ex.Message, "Error");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        private static void OriginDispatcher_Tick(object sender, EventArgs e)
        {
            DispatcherTimer disp = sender as DispatcherTimer;

            GameProcess(originGame).Start();

            Processes.ProcessWatcher pWatcher = new Processes.ProcessWatcher();
            pWatcher.StartWatching(originGame);

            disp.Stop();
        }

        /// <summary>
        /// Returns a process, animates and tries to hook to exited event for the specified game process
        /// </summary>
        /// <param name="game">The game process to launch</param>
        /// <returns></returns>
        private static Process GameProcess(Game game)
        {
            game.BeginStoryboard(game.LaunchStoryboard);

            Process gProcess = new Process();
            gProcess.StartInfo.FileName = game.Path;
            gProcess.StartInfo.Arguments = game.Arguments;
            gProcess.StartInfo.WorkingDirectory = game.WorkDir;
            try
            {
                gProcess.EnableRaisingEvents = true;
                gProcess.Exited += InitialLaunchProcess_Exited;
            }
            catch (Exception)
            {

            }

            return gProcess;
        }

        private static void InitialLaunchProcess_Exited(object sender, EventArgs e)
        {
            //Console.WriteLine("Process exited");
            Process p = sender as Process;
            //if (p != null)
            //{
            //    Console.WriteLine("Process name: " + p.ProcessName);
            //}
            //p.Exited -= InitialLaunchProcess_Exited;
            p?.Dispose();
        }
    }
}
