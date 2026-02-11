using System;
using System.Threading;
using System.Windows;
using SharpDX.XInput;
using static Fuzion.MainWindow;
using static Fuzion.Programs.Launch;
using static Fuzion.Programs.ProgramManager;
using Fuzion.Icons;
using Fuzion.Extensions;
using Fuzion.Native;
using System.Text;

namespace Fuzion.Gamepad
{
    // TO-DO
    // Add Gamepad controls to notification windows!!!
    // Check if I can detect a TV easily and add a setting for that too ^

    internal class Bindings
    {
        //public static void InitializeDirectInput()
        //{
        //    // Initialize DirectInput
        //    var directInput = new DirectInput();

        //    // Find a Joystick Guid
        //    var joystickGuid = Guid.Empty;

        //    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad,
        //                DeviceEnumerationFlags.AllDevices))
        //        joystickGuid = deviceInstance.InstanceGuid;

        //    // If Gamepad not found, look for a Joystick
        //    if (joystickGuid == Guid.Empty)
        //        foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick,
        //                DeviceEnumerationFlags.AllDevices))
        //            joystickGuid = deviceInstance.InstanceGuid;

        //    // If Joystick not found, throws an error
        //    if (joystickGuid == Guid.Empty)
        //    {
        //        Console.WriteLine("No joystick/Gamepad found.");
        //        Console.ReadKey();
        //        Environment.Exit(1);
        //    }

        //    // Instantiate the joystick
        //    var joystick = new Joystick(directInput, joystickGuid);

        //    Console.WriteLine("Found Joystick/Gamepad with GUID: {0}", joystickGuid);

        //    // Query all suported ForceFeedback effects
        //    var allEffects = joystick.GetEffects();
        //    foreach (var effectInfo in allEffects)
        //        Console.WriteLine("Effect available {0}", effectInfo.Name);

        //    // Set BufferSize in order to use buffered data.
        //    joystick.Properties.BufferSize = 128;

        //    // Acquire the joystick
        //    joystick.Acquire();

        //    // Poll events from joystick
        //    while (true)
        //    {
        //        joystick.Poll();
        //        var datas = joystick.GetBufferedData();
        //        foreach (var state in datas)
        //            Console.WriteLine(state);
        //    }
        //}

        static Controller ControllerOne { get; set; }
        static TimerExtended CheckForControllerTimer { get; set; } = new TimerExtended(CheckForGamepad, null, 1000, 1000, false, true);

        public static void InitializeXInput()
        {
            if (Properties.Settings.Default.EnableGamepad)
            {
                CheckForControllerTimer.Start();
            } 
            else
            {
                CheckForControllerTimer.Pause();
            }
        }

        public static void CheckForGamepad(object state)
        {
            //Console.WriteLine("Looking for controller...");
            var controllers = new[] { new Controller(UserIndex.One), new Controller(UserIndex.Two), new Controller(UserIndex.Three), new Controller(UserIndex.Four) };

            // Get 1st controller available
            ControllerOne = null;

            foreach (var controller in controllers)
            {
                if (controller.IsConnected)
                {
                    ControllerOne = controller;
                    break;
                }
            }

            if (ControllerOne == null)
            {
                //Console.WriteLine("No XInput controller detected");
                CheckForControllerTimer.Start();
            }
            else
            {
                Console.WriteLine("Found an XInput controller");
                WindowsManager.OpenWindow.NotificationToast("", "Gamepad Connected",220d, 55d);
                ToggleIconZoom(true);
                CheckForControllerTimer.Pause();
                //PollControllerEvents();

                StartGamepadPolling();
            }
        }

        static TimerExtended GamepadPollingTimer = new TimerExtended(GamepadPollingTimer_Tick, null, 10, 10, false, true);
        static State PreviousState { get; set; }

        private static void StartGamepadPolling()
        {
            // Poll events from joystick
            PreviousState = ControllerOne.GetState();
            GamepadPollingTimer.Start();
            Console.WriteLine("Gamepad Polling Started");
        }

        private static void StopGamepadPolling()
        {
            GamepadPollingTimer.Pause();
            Console.WriteLine("Controller disconnected");
            WindowsManager.OpenWindow.NotificationToast("", "Gamepad Disconnected", 220d, 55d);
            ToggleIconZoom(false);
            Repeater.ToggleInitialDelay(false);
            // start the gamepad connection check timer again
            InitializeXInput();
        }

        private static void GamepadPollingTimer_Tick(object s)
        {
            //Console.WriteLine("Gamepad Poll Tick");
            if (Properties.Settings.Default.EnableGamepad == false)
            {
                // Stop polling if gamepad was disabled from settings
                StopGamepadPolling();
            }

            if (ControllerOne.IsConnected)
            {
                var state = ControllerOne.GetState();
                if (PreviousState.PacketNumber != state.PacketNumber)
                {
                    GamepadPollingTimer.Pause();
                    Console.WriteLine(state.Gamepad);
                    CheckForGuideButtonPress();

                    CheckLeftAnalogReady(state);
                    CheckRepeater(state);
                    NavigateFuzion(state);

                    NavigateFuzionGlobal(state);
                    PreviousState = state;
                    GamepadPollingTimer.Start();
                }
            }
            else // controller disconnected
            {
                StopGamepadPolling();
            }
        }

        static void PollControllerEvents()
        {

            // Poll events from joystick
            var previousState = ControllerOne.GetState();
            while (ControllerOne.IsConnected)
            {
                // Stop polling if gamepad was disabled from settings
                if (Properties.Settings.Default.EnableGamepad == false)
                {
                    break;
                }

                var state = ControllerOne.GetState();
                if (previousState.PacketNumber != state.PacketNumber)
                {
                    //Console.WriteLine(state.Gamepad);
                    CheckForGuideButtonPress();
                    CheckLeftAnalogReady(state);
                    CheckRepeater(state);
                    // need to stop this from fighting with the other call from repeater
                    NavigateFuzion(state);
                    
                    NavigateFuzionGlobal(state);

                }

                // no idea if i need this
                Thread.Sleep(10);

                previousState = state;
            }

            Console.WriteLine("Controller disconnected");
            WindowsManager.OpenWindow.NotificationToast("", "Gamepad Disconnected", 220d, 55d);
            ToggleIconZoom(false);
            // start the timer again
            InitializeXInput();
        }

        public static void ToggleIconZoom(bool connected)
        {
            if (Properties.Settings.Default.ZoomIconsGamepad)
            {
                if (connected)
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        AppWindow.UpdateGameIconSizes(GamepadStatus.Connected);
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AppWindow.UpdateGameIconSizes(GamepadStatus.Disconnected);
                    });
                }
            }
        }

        private static void CheckForGuideButtonPress()
        {
            if (Properties.Settings.Default.EnableGamepadGuideButton
                   && UWPBindings.TestHomeButton() == true
                   && MainWindowActive == false)
            {
                //GamepadPollingTimer.Pause();
                Console.WriteLine("Guide button pressed");
                WindowsManager.OpenWindow.NotificationToast("", "Focusing Fuzion", 220d, 55d);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    TrayIcon.FocusFuzionOnClick();
                });
                //GamepadPollingTimer.Start();
            }
        }

        /// <summary>
        /// Determines whether the specified key is pressed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        ///   <c>true</c> if the specified key is pressed; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsKeyPressed(ConsoleKey key)
        {
            Console.WriteLine("Gamepad key: "+key.ToString());
            return Console.KeyAvailable && Console.ReadKey(true).Key == key;
        }

        private const short leftAnalogSensitivity = short.MaxValue / 2;
        //const short rightAnalogSensitivity = 2500;

        public static bool leftAnalogReady = true;

        private static void CheckLeftAnalogReady(State s)
        {
            if (leftAnalogReady)
            {
                return;
            }

            if (Properties.Settings.Default.DockLocation <= 1)
            {
                if (s.Gamepad.LeftThumbX >= -leftAnalogSensitivity && s.Gamepad.LeftThumbX <= leftAnalogSensitivity)
                {
                    leftAnalogReady = true;
                }
            }
            else
            {
                if (s.Gamepad.LeftThumbY >= -leftAnalogSensitivity && s.Gamepad.LeftThumbY <= leftAnalogSensitivity)
                {
                    leftAnalogReady = true;
                }
            }
        }

                private static bool IsDesktopForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            StringBuilder className = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, className, className.Capacity);
            
            return className.ToString() == "Progman" || className.ToString() == "WorkerW";
        }

        /// <summary>
        /// Forward gamepad button presses only when Fuzion is the foreground app
        /// </summary>
        public static void NavigateFuzion(State s)
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            bool isDesktop = IsDesktopForeground(foreground);
            
            // Allow if:
            // 1. Fuzion is Foreground (Handle match)
            // 2. OR Desktop is Foreground (Progman/WorkerW)
            // If neither is true, we are in another app -> Block Input
            if (foreground != MainWindow.Handle && !isDesktop)
            {
                //Console.WriteLine("Main Window Inactive - not reading Gamepad");
                MainWindow.ForceDeactivate();
                return;
            }

            LastInputSource = InputSource.Gamepad;

            if (leftAnalogReady)
            {
                if(Properties.Settings.Default.DockLocation <= 1)
                {
                    // go left
                    if (s.Gamepad.LeftThumbX < -leftAnalogSensitivity || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                    {
                        // set if null
                        if (HighlightedGame == null && GameObjects.Count != 0)
                        {
                            HighlightedGame = GameObjects[0];
                        }

                        Console.WriteLine("Move left");
                        // move
                        if (HighlightedGame != null && GameObjects.Count != 0)
                        {
                            int nextGameIndex = GameObjects.IndexOf(HighlightedGame) - 1;

                            if (nextGameIndex >= 0)
                            {
                                //HighlightedGame = gameObjects[nextGameIndex];
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    GameObjects[nextGameIndex].Focus();
                                }));

                                leftAnalogReady = false;
                            }
                        }
                    }

                    // go right
                    if (s.Gamepad.LeftThumbX > leftAnalogSensitivity || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                    {
                        // set if null
                        if (HighlightedGame == null && GameObjects.Count != 0)
                        {
                            HighlightedGame = GameObjects[0];
                        }

                        Console.WriteLine("Move right");
                        // move
                        if (HighlightedGame != null && GameObjects.Count != 0)
                        {
                            int nextGameIndex = GameObjects.IndexOf(HighlightedGame) + 1;

                            if (nextGameIndex < GameObjects.Count)
                            {
                                //HighlightedGame = gameObjects[nextGameIndex];

                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    GameObjects[nextGameIndex].Focus();
                                }));

                                leftAnalogReady = false;
                            }
                        }
                    }
                }
                else
                {
                    // go up
                    if (s.Gamepad.LeftThumbY > leftAnalogSensitivity || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
                    {
                        // set if null
                        if (HighlightedGame == null && GameObjects.Count != 0)
                        {
                            HighlightedGame = GameObjects[0];
                        }

                        Console.WriteLine("Move up");
                        // move
                        if (HighlightedGame != null && GameObjects.Count != 0)
                        {
                            int nextGameIndex = GameObjects.IndexOf(HighlightedGame) - 1;

                            if (nextGameIndex >= 0)
                            {
                                //HighlightedGame = gameObjects[nextGameIndex];
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    GameObjects[nextGameIndex].Focus();
                                }));

                                leftAnalogReady = false;
                            }
                        }
                    }

                    // go down
                    if (s.Gamepad.LeftThumbY < -leftAnalogSensitivity || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                    {
                        // set if null
                        if (HighlightedGame == null && GameObjects.Count != 0)
                        {
                            HighlightedGame = GameObjects[0];
                        }

                        Console.WriteLine("Move down");
                        // move
                        if (HighlightedGame != null && GameObjects.Count != 0)
                        {
                            int nextGameIndex = GameObjects.IndexOf(HighlightedGame) + 1;

                            if (nextGameIndex < GameObjects.Count)
                            {
                                //HighlightedGame = gameObjects[nextGameIndex];

                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    GameObjects[nextGameIndex].Focus();
                                }));

                                leftAnalogReady = false;
                            }
                        }
                    }
                }
            }

            if(Properties.Settings.Default.DockLocation <= 1)
            {
                // Horizontal Scroll using right analog
                if (s.Gamepad.RightThumbX < -Properties.Settings.Default.RightAnalogSensitivity || s.Gamepad.RightThumbX > Properties.Settings.Default.RightAnalogSensitivity)
                    ScrollDock(s);
            }
            else
            {
                // Vertical Scroll using right analog
                if (s.Gamepad.RightThumbY < -Properties.Settings.Default.RightAnalogSensitivity || s.Gamepad.RightThumbY > Properties.Settings.Default.RightAnalogSensitivity)
                    ScrollDock(s);
            }


            if (s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
            {
                Console.WriteLine("Start game normal");
                if(HighlightedGame != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        LaunchGame(HighlightedGame);
                    }));
                }
            }

            if (s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
            {
                Console.WriteLine("Start game CLoE");
                if (HighlightedGame != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        LaunchGame(HighlightedGame, true);
                    }));
                }
            }
        }

        static void CheckRepeater(State s)
        {
            
            // left right
            if(Properties.Settings.Default.DockLocation == 0 || Properties.Settings.Default.DockLocation == 1)
            {
                // Start initial timer
                if (s.Gamepad.LeftThumbX > leftAnalogSensitivity
                    || s.Gamepad.LeftThumbX < -leftAnalogSensitivity
                    || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)
                    || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                {
                    Repeater.ControllerState = s;
                    Repeater.ToggleInitialDelay(true);

                }
                else
                {
                    Repeater.ToggleInitialDelay(false);
                }
            }
            else //up down
            {
                // Start initial timer
                if (s.Gamepad.LeftThumbY > leftAnalogSensitivity
                    || s.Gamepad.LeftThumbY < -leftAnalogSensitivity
                    || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)
                    || s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                {
                    Repeater.ControllerState = s;
                    Repeater.ToggleInitialDelay(true);

                }
                else
                {
                    Repeater.ToggleInitialDelay(false);
                }
            }
        }

        /// <summary>
        /// Forward gamepad button presses from anywhere within the OS
        /// </summary>
        static void NavigateFuzionGlobal(State s)
        {
            // Take it out so it can be used globally
            // Task switcher flashes a bit which can be annoying, but I think I'll not mess
            // with it for now
            
            // This button is used for many games and it won't work if it's bound when playing (maybe only when not playing)
            //if (s.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
            //{
            //    // Alt+tab
            //    System.Windows.Forms.SendKeys.SendWait("%{Tab}");
            //}

        }

        static void ScrollDock(State s)
        {
            double increment;

            if (Properties.Settings.Default.DockLocation <= 1)
            {
                increment = NormalizeAxis(s.Gamepad.RightThumbX) * 0.25d; // the double is sensitivity multiplier
                Dock.Scrolling.ScrollTo(Dock.Scrolling.SmoothScrollTarget + increment);
            }
            else
            {
                increment = NormalizeAxis(s.Gamepad.RightThumbY) * 0.25d; // the double is sensitivity multiplier
                Dock.Scrolling.ScrollTo(Dock.Scrolling.SmoothScrollTarget - increment);
            }

            Console.WriteLine("Increment is "+ increment);



            //if (e.Delta < 0 && smoothScrollTarget < 1) //delta is negative
            //{
            //    smoothScrollTarget += increment;
            //}

            //if (e.Delta > 0 && smoothScrollTarget > 0) //delta is positive
            //{
            //    smoothScrollTarget -= increment;
            //}

            if (Dock.Scrolling.SmoothScrollTarget < 0)
            {
                Dock.Scrolling.ScrollTo(0);
            }

            if (Dock.Scrolling.SmoothScrollTarget > Dock.Scrolling.ScrollableMax())
            {
                Dock.Scrolling.ScrollTo(Dock.Scrolling.ScrollableMax());
            }
        }

        static double NormalizeAxis(short s)
        {
            return (double)((1d / 32768d) * s);
        }
    }
}
