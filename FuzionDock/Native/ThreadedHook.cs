using H.Hooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fuzion.Native.IdleHook;

namespace Fuzion.Native
{
    class ThreadedHook
    {
        public static LowLevelKeyboardHook KeyHook { get; } = GetKeyHook();
        public static LowLevelKeyboardHook AppKeyHook { get; } = GetKeyHook();
        public static LowLevelMouseHook MouseHook { get; } = GetMouseHook();

        static LowLevelKeyboardHook GetKeyHook()
        {
            var keyboardHook = new LowLevelKeyboardHook();
            return keyboardHook;
        }

        static LowLevelMouseHook GetMouseHook()
        {
            var mouseHook = new LowLevelMouseHook();
            return mouseHook;
        }

        public static void EnableKeyboardHook()
        {
            KeyHook.Down += KeyHook_Down;
            KeyHook.Up += KeyHook_Up;

            KeyHook.Handling = true;
            KeyHook.IsExtendedMode = true;
            

            KeyHook.Start();
        }

        public static void EnableMandatoryHooks()
        {
            EnableKeyboardHook();
            MouseHook.Move += MouseHook_Move;

            MouseHook.GenerateMouseMoveEvents = true;
            MouseHook.Start();
        }

        public static void EnableShadowLaunchHooks()
        {
            MouseHook.Down += MouseHook_Down;
            MouseHook.Wheel += MouseHook_Wheel;
        }

        public static void DisableShadowLaunchHooks()
        {
            MouseHook.Down -= MouseHook_Down;
            MouseHook.Wheel -= MouseHook_Wheel;
        }

        public static void EnableMouseHook()
        {
            MouseHook.Move += MouseHook_Move;
            MouseHook.Down += MouseHook_Down;
            MouseHook.Wheel += MouseHook_Wheel;

            MouseHook.GenerateMouseMoveEvents = true;

            MouseHook.Start();
        }

        public static void DisableKeyboardHook()
        {
            KeyHook.Down -= KeyHook_Down;
            KeyHook.Up -= KeyHook_Up;
        
            KeyHook.Stop();
        }

        public static void DisableMouseHook()
        {
            MouseHook.Move -= MouseHook_Move;
            MouseHook.Down -= MouseHook_Down;
            MouseHook.Wheel -= MouseHook_Wheel;
            
            MouseHook.Stop();
        }

        public static void DisableAllHooks()
        {
            KeyHook.Down -= KeyHook_Down;
            KeyHook.Up -= KeyHook_Up;

            MouseHook.Move -= MouseHook_Move;
            MouseHook.Down -= MouseHook_Down;
            MouseHook.Wheel -= MouseHook_Wheel;

            KeyHook.Stop();
            MouseHook.Stop();
        }

        private static void MouseHook_Wheel(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("Mouse Wheel");
            IdleTime.Reset();
        }

        private static void MouseHook_Down(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("Mouse down: "+e.Keys.ToString());
            IdleTime.Reset();
        }

        private static void MouseHook_Move(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("Mouse Move");
            IdleTime.Reset();

            if (MainWindow.LastInputSource != MainWindow.InputSource.Mouse)
            {
                MainWindow.LastInputSource = MainWindow.InputSource.Mouse;
            }

        }

        private static void KeyHook_Up(object sender, KeyboardEventArgs e)
        {
            //Console.WriteLine("Pressing arrow nav from active MW");
            //if (e.Keys.Values.Contains(Key.Left))
            //{
            //    System.Threading.ThreadPool.QueueUserWorkItem(ThreadSafeArrowNav, System.Windows.Forms.Keys.Left);
            //    e.IsHandled = true;
            //}
            //else if (e.Keys.Values.Contains(Key.Right))
            //{
            //    System.Threading.ThreadPool.QueueUserWorkItem(ThreadSafeArrowNav, System.Windows.Forms.Keys.Right);
            //    e.IsHandled = true;
            //}
            //else if (e.Keys.Values.Contains(Key.Up))
            //{
            //    System.Threading.ThreadPool.QueueUserWorkItem(ThreadSafeArrowNav, System.Windows.Forms.Keys.Up);
            //    e.IsHandled = true;
            //}
            //else if (e.Keys.Values.Contains(Key.Down))
            //{
            //    System.Threading.ThreadPool.QueueUserWorkItem(ThreadSafeArrowNav, System.Windows.Forms.Keys.Down);
            //    e.IsHandled = true;
            //}

        }

        static void ThreadSafeArrowNav(Object stateInfo)
        {
            var key = (System.Windows.Forms.Keys)stateInfo;
            MainWindow.ArrowNavigationPressedGlobal(false, key, false);
        }

        private static void KeyHook_Down(object sender, KeyboardEventArgs e)
        {
            //Console.WriteLine("Key Hook Down: " + e.Keys.ToString());

            IdleTime.Reset();

            if (e.Keys.IsCtrl && e.Keys.Values.Contains(Key.OemTilde))
            {
                Console.WriteLine("Focus Fuzion from Ctrl+Tilde");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Icons.TrayIcon.FocusFuzionOnClick();
                });

            }
        }
    }
}
