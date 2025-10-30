using System;
using Fuzion.LauncherSpecific;
using System.Windows.Threading;
using System.Diagnostics;
using static Fuzion.Native.IdleHook.HookManager;
using System.Linq;
using Fuzion.Properties;
using System.Windows.Forms;

namespace Fuzion.Native.IdleHook
{

    // Detect global idle time with mouse and keyboard hook
    // https://weblogs.asp.net/jdanforth/detecting-idle-time-with-global-mouse-and-keyboard-hooks-in-wpf

    static class IdleTime
    {
        /// <summary>
        /// Time in seconds since the user last interacted with the Mouse or Keyboard
        /// </summary>
        public static int TimeIdle { get; private set; }
        public static DispatcherTimer ElapsedTime { get; } = MainTimer();
        /// <summary>
        /// Delay other launchers for this many seconds
        /// </summary>
        public static int IdleThreshold
        {
            get { return Settings.Default.IdleTimeSL * 60; }
        }

        public static bool[] allShadowLaunchers = new bool[]
        {
            Settings.Default.IsSteamSL,
            Settings.Default.IsOriginSL,
            Settings.Default.IsEpicSL,
            Settings.Default.IsUplaySL,
            Settings.Default.IsGoGSL,
            Settings.Default.IsBattleNetSL
        };

        public static void Start()
        {
            if (!ElapsedTime.IsEnabled && allShadowLaunchers.Any(enabled => enabled == true))
            {
                Console.WriteLine("Starting Idle Timer");

                ElapsedTime.Start();
                ThreadedHook.EnableShadowLaunchHooks();
            }
        }

        public static void Stop()
        {
            if (ElapsedTime.IsEnabled)
            {
                Console.WriteLine("Stopping Idle Timer");
                ElapsedTime.Stop();

                ThreadedHook.DisableShadowLaunchHooks();
            }

        }

        private static DispatcherTimer MainTimer()
        {
            DispatcherTimer dp = new DispatcherTimer();
            dp.Interval = TimeSpan.FromSeconds(10d);
            dp.Tick += ElapsedTime_Tick;
            return dp;
        }

        public static void Reset()
        {
            if(ElapsedTime.IsEnabled)
                TimeIdle = 0;
        }

        private static void ElapsedTime_Tick(object sender, EventArgs e)
        {
            TimeIdle += 10;
            Console.WriteLine($"User has been idle for {TimeIdle} seconds");

            if (TimeIdle >= IdleThreshold)
            {
                Process[] processes = Process.GetProcesses();

                if (Settings.Default.IsSteamSL)
                {
                    if (!processes.Any(process => process.ProcessName == Steam.ClientProcessName))
                    {
                        StartLauncher(Steam.Path, Steam.Arguments);
                    }
                }

                if (Settings.Default.IsEpicSL)
                {
                    if (!processes.Any(process => process.ProcessName == EpicGames.ClientProcessName))
                    {
                        StartLauncher(EpicGames.Path, EpicGames.Arguments);
                    }
                }

                if (Settings.Default.IsGoGSL)
                {
                    if (!processes.Any(process => process.ProcessName == GOG.ClientProcessName))
                    {
                        StartLauncher(GOG.Path, GOG.Arguments);
                    }
                }

                if (Settings.Default.IsOriginSL)
                {
                    if (!processes.Any(process => process.ProcessName == Origin.ClientProcessName))
                    {
                        StartLauncher(Origin.Path, Origin.Arguments, Origin.WorkDir);
                        Origin.MinimizeToTray();
                    }
                }

                if (Settings.Default.IsBattleNetSL)
                {
                    if (!processes.Any(process => process.ProcessName == BattleNet.ClientProcessName))
                    {
                        StartLauncher(BattleNet.Path, BattleNet.Arguments);
                    }
                }

                if (Settings.Default.IsUplaySL)
                {
                    if (!processes.Any(process => process.ProcessName == Uplay.ClientProcessName))
                    {
                        StartLauncher(Uplay.Path, Uplay.Arguments);
                    }
                }

                Stop();
            } 
        }

        private static void StartLauncher(string path, string arguments, string workDir = "")
        {
            try
            {
                Process pro = new Process();
                pro.StartInfo.FileName = path;
                pro.StartInfo.WorkingDirectory = workDir;//System.IO.Path.GetDirectoryName(path);
                Console.WriteLine("Workdir for launcher is: " + pro.StartInfo.WorkingDirectory);
                pro.StartInfo.Arguments = arguments;
                pro.Start();
                pro.Dispose();
            }
            catch (Exception)
            {

            }
           
        }
    }
}
