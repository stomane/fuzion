using Fuzion.Icons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using static Fuzion.Native.NativeMethods;

namespace Fuzion.Native
{
    class GlobalHotkeyManager
    {
        #region Global Hotkey Registration
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;

        //Modifiers:
        private const uint MOD_NONE = 0x0000; //(none)
        private const uint MOD_ALT = 0x0001; //ALT
        private const uint MOD_CONTROL = 0x0002; //CTRL
        private const uint MOD_SHIFT = 0x0004; //SHIFT
        private const uint MOD_WIN = 0x0008; //WINDOWS
        //CAPS LOCK:
        private const uint VK_CAPITAL = 0x14;
        private const uint VK_OEM_3 = 0xC0;


        #endregion

        private IntPtr _windowHandle;
        private HwndSource _source;

        public GlobalHotkeyManager()
        {
            _windowHandle = new WindowInteropHelper(MainWindow.AppWindow).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);

            RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL, VK_OEM_3); //CTRL + TILDE (~)
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            switch (msg)
            {
                case WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        case HOTKEY_ID:
                            int vkey = (((int)lParam >> 16) & 0xFFFF);
                            if (vkey == VK_OEM_3)
                            {
                                //tblock.Text += "CapsLock was pressed" + Environment.NewLine;

                                if (MainWindow.AppWindow.IsActive)
                                {
                                    //MainWindow.AppWindow.Topmost = false;
                                } else
                                {
                                    Console.WriteLine("Activating from Hotkey: CTRL + ~");
                                    //MainWindow.AppWindow.Activate();
                                    TrayIcon.FocusFuzionOnClick();
                                }

                            }
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        public void UnregisterGlobalHotkey()
        {
            _source.RemoveHook(HwndHook);
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
        }
    }
}
