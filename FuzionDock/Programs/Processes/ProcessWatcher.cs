using Fuzion.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace Fuzion.Programs.Processes
{
    class ProcessWatcher : IDisposable
    {
        public Game AssociatedGame { get; set; }
        public DispatcherTimer FindProcessDisp { get; set; }
        public DispatcherTimer StopLookingForProcessDisp { get; set; }
        public DispatcherTimer ManualProcessExitDisp { get; set; }

        public DispatcherTimer DelayedLauncherCloseDisp { get; set; }
        public int OriginCounter { get; set; } = 0;
        public bool OriginHooked { get; set; } = false;
        public Process WatchedProcess { get; set; }
        public string LastWatchedProcessName { get; set; }

        public void StartWatching(Game game)
        {
            // If we fail to hook to exit event, check every minute if the process is running
            ManualProcessExitDisp = new DispatcherTimer();
            ManualProcessExitDisp.Interval = TimeSpan.FromSeconds(15d);
            ManualProcessExitDisp.Tick += ManualProcessExitDisp_Tick;

            FindProcessDisp = new DispatcherTimer();
            FindProcessDisp.Interval = TimeSpan.FromSeconds(1d);
            FindProcessDisp.Tick += FindGameProcessDisp_Tick;

            StopLookingForProcessDisp = new DispatcherTimer();
            StopLookingForProcessDisp.Interval = TimeSpan.FromSeconds(180d);
            StopLookingForProcessDisp.Tick += StopLookingForProcessDisp_Tick;

            DelayedLauncherCloseDisp = new DispatcherTimer();
            DelayedLauncherCloseDisp.Interval = TimeSpan.FromSeconds(5d);
            DelayedLauncherCloseDisp.Tick += DelayedLauncherCloseDisp_Tick;

            AssociatedGame = game;
            FindProcessDisp.Start();
        }

        private void ManualProcessExitDisp_Tick(object sender, EventArgs e)
        {
            if (Process.GetProcesses().Any(p => p.ProcessName == LastWatchedProcessName))
            {
                Console.WriteLine("Process is still running: " + LastWatchedProcessName);
            }
            else
            {
                Console.WriteLine("Manual detected process closed");
                WatchedProcess_Exited(this, new EventArgs());
                ManualProcessExitDisp.Stop();
                OriginHooked = false;
            }
        }

        private void FindGameProcessDisp_Tick(object sender, EventArgs e)
        {
            WatchedProcess = FindGameProcess();

            if (WatchedProcess != null)
            {
                Console.WriteLine("Watched process is there and is: " + WatchedProcess.ProcessName);
                if (/*LastWatchedProcess == null && */LastWatchedProcessName != WatchedProcess.ProcessName || OriginCounter == 1 && !OriginHooked)
                {
                    // The process has changed
                    ManualProcessExitDisp.Stop();
                    OriginHooked = false;
                    LastWatchedProcessName = WatchedProcess.ProcessName;
                    StopLookingForProcessDisp.Stop();
                    try
                    {
                        WatchedProcess.EnableRaisingEvents = true;
                        WatchedProcess.Exited += WatchedProcess_Exited;
                        if (OriginCounter == 1)
                            OriginHooked = true;

                        Console.WriteLine("Exit event hooked successfully");
                    }
                    catch (Exception)
                    {
                        // works with admin
                        Console.WriteLine("Exit event hook failed");

                        if (!ManualProcessExitDisp.IsEnabled)
                        {
                            Console.WriteLine("Starting manual check");

                            StopLookingForProcessDisp.Start();
                            ManualProcessExitDisp.Start();
                            OriginHooked = true;
                        }

                    }

                }
                else
                {
                    if (!StopLookingForProcessDisp.IsEnabled)
                    {
                        Console.WriteLine("Starting stop looking for process");
                        StopLookingForProcessDisp.Start();
                    }
                }

            }
            else
            {
                if (!StopLookingForProcessDisp.IsEnabled)
                {
                    Console.WriteLine("Starting stop looking for process");
                    StopLookingForProcessDisp.Start();
                }

                //if (!ManualProcessExitDisp.IsEnabled)
                //{
                Console.WriteLine("Watched process is NOT there");
                //}
            }
        }
        private void StopLookingForProcessDisp_Tick(object sender, EventArgs e)
        {
            // Final process is captured
            Console.WriteLine("Stopping process finder dispatcher");
            FindProcessDisp.Stop();
            StopLookingForProcessDisp.Stop();
            //Dispose();
        }

        private void DelayedLauncherCloseDisp_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("<<<Watched Process Exited Info>>>");
            Console.WriteLine("Game exited with process name: " + LastWatchedProcessName);
            //Console.WriteLine("Attempting to close launcher: " + AssociatedGame.Launcher.ToString());

            if (AssociatedGame.Launcher == BelongsToLauncher.Epic)
            {
                LauncherSpecific.EpicGames.Close();
            }

            if (AssociatedGame.Launcher == BelongsToLauncher.Steam)
            {
                LauncherSpecific.Steam.Close();
            }

            if (AssociatedGame.Launcher == BelongsToLauncher.Uplay)
            {
                LauncherSpecific.Uplay.Close();
            }

            if (AssociatedGame.Launcher == BelongsToLauncher.Origin)
            {
                LauncherSpecific.Origin.Close();
            }

            Dispose();

        }

        // This can be improved to use the Delayed Launcher Close Dispatcher instead of the origincounter and originhooked vars
        private void WatchedProcess_Exited(object sender, EventArgs e)
        {
            if (/*AssociatedGame.Launcher == BelongsToLauncher.Origin || */AssociatedGame.Launcher == BelongsToLauncher.Uplay
          && OriginCounter == 0 && !OriginHooked)
            {
                // Origin launches the game and stops the process right away for some reason
                OriginCounter++;
                Console.WriteLine("Origin counter is now " + OriginCounter);
            }
            else
            {
                if (AssociatedGame.Launcher == BelongsToLauncher.Epic)
                {
                    DelayedLauncherCloseDisp.Interval = TimeSpan.FromSeconds(0d);
                }

                if (AssociatedGame.Launcher == BelongsToLauncher.Steam)
                {
                    DelayedLauncherCloseDisp.Interval = TimeSpan.FromSeconds(0d);
                }

                if (AssociatedGame.Launcher == BelongsToLauncher.Uplay)
                {
                    DelayedLauncherCloseDisp.Interval = TimeSpan.FromSeconds(0d);
                }

                if (AssociatedGame.Launcher == BelongsToLauncher.Origin)
                {
                    DelayedLauncherCloseDisp.Interval = TimeSpan.FromSeconds(5d);
                }

                DelayedLauncherCloseDisp.Start();
            }
        }

        private Process FindGameProcess()
        {
            if (!string.IsNullOrEmpty(AssociatedGame.WorkDir))
            {
                string[] workDirFiles = Directory.GetFiles(AssociatedGame.WorkDir, "*.exe");
                Process[] pcs = Process.GetProcesses();

                if (workDirFiles.Length != 0)
                {
                    for (int x = 0; x < workDirFiles.Length; x++)
                    {
                        for (int y = 0; y < pcs.Length; y++)
                        {
                            if (Path.GetFileNameWithoutExtension(workDirFiles[x]) == pcs[y].ProcessName)
                            {
                                return pcs[y];
                            }
                        }
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            DelayedLauncherCloseDisp.Stop();
            FindProcessDisp.Stop();
            StopLookingForProcessDisp.Stop();
            ManualProcessExitDisp.Stop();
            WatchedProcess?.Dispose();
        }

        //private static Process FindGameProcess(Game game)
        //{
        //    Process[] processes = Process.GetProcesses();
        //    string sysIconExeName = "";

        //    try
        //    {
        //        if (!string.IsNullOrEmpty(game.SystemIcon))
        //            sysIconExeName = System.IO.Path.GetFileNameWithoutExtension(game.SystemIcon);
        //    }
        //    catch (Exception)
        //    {

        //    }

        //    foreach (Process p in processes)
        //    {
        //        if (p.ProcessName.ToUpperInvariant() == game.DisplayName.ToUpperInvariant()
        //            || p.ProcessName.ToLowerNormalized().ContainsMostWords(game.DisplayName.ToLowerNormalized(), 50)
        //            || p.ProcessName.ToUpperInvariant() == game.ExeName.ToUpperInvariant()
        //            || p.ProcessName.ToUpperInvariant() == sysIconExeName.ToUpperInvariant()
        //            || p.ProcessName.ToUpperInvariant() == game.Name.ToAcronym().ToUpperInvariant())
        //        {
        //            return p;
        //        }
        //    }

        //    return null;
        //}

        //[Flags]
        //private enum ProcessAccessFlags : uint
        //{
        //    QueryLimitedInformation = 0x00001000
        //}

        //[DllImport("kernel32.dll", SetLastError = true)]
        //private static extern bool QueryFullProcessImageName(
        //      [In] IntPtr hProcess,
        //      [In] int dwFlags,
        //      [Out] StringBuilder lpExeName,
        //      ref int lpdwSize);

        //[DllImport("kernel32.dll", SetLastError = true)]
        //private static extern IntPtr OpenProcess(
        // ProcessAccessFlags processAccess,
        // bool bInheritHandle,
        // int processId);

        //private string GetProcessFilename(Process p)
        //{
        //    int capacity = 2000;
        //    StringBuilder builder = new StringBuilder(capacity);
        //    IntPtr ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
        //    if (!QueryFullProcessImageName(ptr, 0, builder, ref capacity))
        //    {
        //        return string.Empty;
        //    }

        //    return builder.ToString();
        //}
    }
}
