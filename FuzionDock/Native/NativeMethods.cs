//using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Foundation.Metadata;
using static Fuzion.MainWindow;

namespace Fuzion.Native
{
    internal static class NativeMethods
    {
        #region Blur specific
        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_BLURBEHIND
        {
            public DWM_BB dwFlags;
            public bool fEnable;
            public IntPtr hRgnBlur;
            public bool fTransitionOnMaximized;
        }

        [Flags]
        public enum DWM_BB
        {
            ENABLE = 1,
            BLURREGION = 2,
            TRANSITIONONMAXIMIZED = 4
        }

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern bool DwmIsCompositionEnabled();

        [DllImport("dwmapi.dll", PreserveSig = false)]
        public static extern void DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);
        #endregion

        private static bool canSetTopmost;

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        private static WinEventDelegate stickyDelegate = null;
        private static IntPtr m_hhook = IntPtr.Zero;

        public static string activeWindowName;
        public static string lastWindowClassName;

        #region Windows of Process enumerator

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        public static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        #endregion

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SW_RESTORE = 9;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOWDEFAULT = 10;
        public const int SW_SHOWNORMAL = 1;

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(HandleRef hWnd, int nCmdShow);

        #region Active Window Hook

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public static bool stickToDesktopActive = false;

        // Old method with WinEventHook
        public static void ActivateStickToDesktop()
        {
            if (stickToDesktopActive == false)
            {
                stickyDelegate = new WinEventDelegate(WinEventProc);
                m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, stickyDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
                stickToDesktopActive = true;
            }

        }

        // needs improvement - https://stackoverflow.com/questions/6193711/call-has-been-made-on-garbage-collected-delegate-in-c
        public static void DeactivateStickToDesktop()
        {
            m_hhook = IntPtr.Zero;
            stickyDelegate = null;
        }

        static DispatcherTimer procCounter = ProcTickCounter();
        
        static DispatcherTimer ProcTickCounter()
        {
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(0);
            dt.Tick += Proc_Tick;

            return dt;
        }

        private static void Proc_Tick(object sender, EventArgs e)
        {
            //DispatcherTimer dt = sender as DispatcherTimer;

            ApplyTopmost();
            procCounter.Stop();
            Console.WriteLine("Proctimer stopping");
        }       

        public static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            //// Dispatcher version
            //// First time in a while
            //if (procCounter.IsEnabled == true)
            //{
            //    procCounter.Stop();
            //    procCounter.Start();
                   
            //    Console.WriteLine("Continuing Proc Timer");
            //    //procCounter.Start();
            //    activeWindowName = GetActiveWindowTitle();
            //    Console.WriteLine("Active window: " + activeWindowName);
            //    lastWindowClassName = GetActiveWindowClass();

            //} else
            //{
            //    Console.WriteLine("Starting Proc Timer");
            //    procCounter.Start();
            //    activeWindowName = GetActiveWindowTitle();
            //    Console.WriteLine("Active window: " + activeWindowName);
            //    lastWindowClassName = GetActiveWindowClass();
            //}

            // Instant version
            activeWindowName = GetActiveWindowTitle();
            //Console.WriteLine("Active window: " + activeWindowName);
            lastWindowClassName = GetActiveWindowClass();

            //ApplyTopmost(); //was enabled


            //SetOnDesktop(AppWindow);
        }

        //[DllImport("user32.dll")]
        //static extern int GetForegroundWindow();
        private static int shellTrayWndCount = 0;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        const int ASFW_ANY = -1; // by MSDN

        public static void FuzionCanSetForegroundWindow()
        {
            bool allowed = AllowSetForegroundWindow(System.Diagnostics.Process.GetCurrentProcess().Id);
            //Console.WriteLine("AllowSetForegroundWindow returned: "+allowed);
            //System.Windows.Forms.MessageBox.Show("AllowSetForegroundWindow returned: " + allowed);
        }

        public static void ForceSetForegroundWindow(Window wnd)
        {
            IntPtr hWnd = new WindowInteropHelper(wnd).Handle;
            Console.WriteLine("Foreground window BEFORE is: " + GetForegroundWindow());

            // Original force foreground
            SetForegroundWindowInternal(hWnd);

            // Alternative force foreground
            //ActivateWindowForce(wnd);

            Console.WriteLine("Foreground window is: "+GetForegroundWindow());

            // Current activation because Fuzion is parented to low level desktop window
            AppWindow.MainWindow_Activated(null, null);
        }

        public static void ForceSetForegroundWindow(IntPtr ptr)
        {
            SetForegroundWindow(ptr);
            Console.WriteLine("Foreground window is: " + GetForegroundWindow());
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(int hWnd, StringBuilder lpClassName, int nMaxCount);

        private static void ApplyTopmost()
        {

            if (activeWindowName == null)
            {
                // wait for next call to see if it's still traywnd
                if (lastWindowClassName == "Shell_TrayWnd")
                {
                    // first occurence
                    if (shellTrayWndCount == 0)
                    {
                        Console.WriteLine("Adding Count");
                        shellTrayWndCount++;
                    }
                    else // second in a row
                    {
                        Console.WriteLine("Topmosting from Count");
                        canSetTopmost = true;
                        shellTrayWndCount = 0;
                    }
                }
                else if (lastWindowClassName == "Progman"
                    || lastWindowClassName == "WorkerW"
                    || lastWindowClassName.Contains("WindowsForms10.Window.0.app"))
                {
                    //Console.WriteLine("WINDOW NULL TRUE");
                    canSetTopmost = true;
                    shellTrayWndCount = 0;
                }
                else
                {
                    shellTrayWndCount = 0;
                }
            }
            else if (activeWindowName == "Fuzion Dock")
            {
                canSetTopmost = true;
                shellTrayWndCount = 0;
            }
            else
            {
                if (lastWindowClassName.Contains("Fuzion") || activeWindowName.Contains("Fuzion"))
                {
                    canSetTopmost = true;
                }
                else
                {
                    canSetTopmost = false;
                }

                shellTrayWndCount = 0;
            }

            Console.WriteLine("TRAYWND Count: " + shellTrayWndCount);


            if (canSetTopmost)
            {
                if (!AppWindow.Topmost)
                {
                    Console.WriteLine("TOPPING");
                    AppWindow.Topmost = true;
                    //SendToBottom(AppWindow);
                }

            }
            else
            {
                if (AppWindow.Topmost)
                {
                    Console.WriteLine("BOTTOMING");
                    AppWindow.Topmost = false;
                    SendToBottom(AppWindow);
                }

            }

            //lastWindowClassName = lastWindowClassName;
        }

        //private static void ApplyTopmost()
        //{

        //    if (activeWindowName == null)
        //    {
        //        // wait for next call to see if it's still traywnd
        //        if(lastWindowClassName == "Shell_TrayWnd")
        //        {
        //            // first occurence
        //            if(shellTrayWndCount == 0)
        //            {
        //                Console.WriteLine("Adding Count");
        //                shellTrayWndCount++;
        //            }
        //            else // second in a row
        //            {
        //                Console.WriteLine("Topmosting from Count");
        //                System.Threading.Thread.Sleep(50);
        //                canSetTopmost = true;
        //                shellTrayWndCount = 0;
        //            }
        //        } else
        //        {
        //            // if second occurence is not shell_traywnd, reset counter
        //            shellTrayWndCount = 0;
        //        }

        //        Console.WriteLine("TRAYWND Count: "+shellTrayWndCount);

        //        if (lastWindowClassName == "Progman" 
        //            || lastWindowClassName == "WorkerW" 
        //            || lastWindowClassName.Contains("WindowsForms10.Window.0.app"))
        //            //== "WindowsForms10.Window.0.app.0.e6a20c_r8_ad1") //fuzion tray icon window class name which changes every time
        //        {
        //            //Console.WriteLine("WINDOW NULL TRUE");
        //            canSetTopmost = true;
        //        }
        //        else
        //        {
        //            //Console.WriteLine("WINDOW NULL FALSE");
        //            canSetTopmost = false;
        //        }
        //    }
        //    else if (activeWindowName == "Fuzion Dock")
        //    {
        //        //Console.WriteLine("WINDOW FUZION TRUE");
        //        canSetTopmost = true;
        //    }
        //    else
        //    {
        //        if (lastWindowClassName.Contains("Fuzion") || activeWindowName.Contains("Fuzion"))
        //        {
        //            canSetTopmost = true;
        //        }
        //        else
        //        {
        //            canSetTopmost = false;
        //        }
        //    }


        //    if (canSetTopmost)
        //    {
        //        if (!AppWindow.Topmost)
        //        {
        //            Console.WriteLine("TOPPING");
        //            AppWindow.Topmost = true;
        //            //SendToBottom(AppWindow);
        //        }

        //    }
        //    else
        //    {
        //        if (AppWindow.Topmost)
        //        {
        //            Console.WriteLine("BOTTOMING");
        //            AppWindow.Topmost = false;
        //            SendToBottom(AppWindow);
        //        }

        //    }

        //    //lastWindowClassName = lastWindowClassName;
        //}

        private static string GetActiveWindowClass()
        {
            const int maxChars = 256;
            IntPtr handle = IntPtr.Zero;
            StringBuilder className = new StringBuilder(maxChars);

            handle = GetForegroundWindow();

            if (GetClassName(handle.ToInt32(), className, maxChars) > 0)
            {
                Console.WriteLine("Class name: " + className.ToString());
                return className.ToString();
            }

            return string.Empty;
        }

        #region Window Title
        // Old method
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        private static string GetActiveWindowTitle() //Could change it to suit my needs better, it's good for now. : The name return value is useful if I need to expand on when the MW is topmost
        {
            const int nChars = 256;
            IntPtr handle = IntPtr.Zero;
            StringBuilder Buff = new StringBuilder(nChars);
            handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                Console.WriteLine("Active Window Title: " + Buff.ToString());
                return Buff.ToString();
            }

            Console.WriteLine("Active Window Title IS NULL");
            return null;
        }
        #endregion
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public const UInt32 SWP_NOSIZE = 0x0001;
        public const UInt32 SWP_NOMOVE = 0x0002;
        public const UInt32 SWP_NOZORDER = 0x0004;

        static readonly IntPtr HWND_TOP = new IntPtr(0);
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public static bool SetWindowPosNative(Window wnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags)
        {
            return SetWindowPos(new WindowInteropHelper(wnd).Handle, hWndInsertAfter, X, Y, cx, cy, uFlags);
        }

        static void SendToBottom(Window window)
        {
            var hWnd = new WindowInteropHelper(window).Handle;
            SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
        }

        #endregion

        /// <summary>
        ///     The MoveWindow function changes the position and dimensions of the specified window. For a top-level window, the
        ///     position and dimensions are relative to the upper-left corner of the screen. For a child window, they are relative
        ///     to the upper-left corner of the parent window's client area.
        ///     <para>
        ///     Go to https://msdn.microsoft.com/en-us/library/windows/desktop/ms633534%28v=vs.85%29.aspx for more
        ///     information
        ///     </para>
        /// </summary>
        /// <param name="hWnd">C++ ( hWnd [in]. Type: HWND )<br /> Handle to the window.</param>
        /// <param name="X">C++ ( X [in]. Type: int )<br />Specifies the new position of the left side of the window.</param>
        /// <param name="Y">C++ ( Y [in]. Type: int )<br /> Specifies the new position of the top of the window.</param>
        /// <param name="nWidth">C++ ( nWidth [in]. Type: int )<br />Specifies the new width of the window.</param>
        /// <param name="nHeight">C++ ( nHeight [in]. Type: int )<br />Specifies the new height of the window.</param>
        /// <param name="bRepaint">
        ///     C++ ( bRepaint [in]. Type: bool )<br />Specifies whether the window is to be repainted. If this
        ///     parameter is TRUE, the window receives a message. If the parameter is FALSE, no repainting of any kind occurs. This
        ///     applies to the client area, the nonclient area (including the title bar and scroll bars), and any part of the
        ///     parent window uncovered as a result of moving a child window.
        /// </param>
        /// <returns>
        ///     If the function succeeds, the return value is nonzero.<br /> If the function fails, the return value is zero.
        ///     <br />To get extended error information, call GetLastError.
        /// </returns>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);


        [DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private static void RefreshDesktop()
        {
            _ = SHChangeNotify(0x8000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
        }

        #region Window styles
        [Flags]
        public enum ExtendedWindowStyles
        {
            // ...
            WS_EX_TOOLWINDOW = 0x00000080,
            // ...
        }

        public enum GetWindowLongFields
        {
            // ...
            GWL_EXSTYLE = (-20),
            // ...
        }

        //[DllImport("user32.dll")]
        //public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;
            // Win32 SetWindowLong doesn't clear error on success
            SetLastError(0);

            if (IntPtr.Size == 4)
            {
                // use SetWindowLong
                Int32 tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(tempResult);
            }
            else
            {
                // use SetWindowLongPtr
                result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);
        #endregion


        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static IntPtr IntermediateWorkerWPointer { get; } = GetIntermediateWorkerW(); // set in GetIntermediateWorkerW()

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        // This static method is required because Win32 does not support
        // GetWindowLongPtr directly
        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool BringWindowToTop(HandleRef hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // Make sure RECT is actually OUR defined struct, not the windows rect.
        public static RECT GetWindowRectangle(Window window)
        {
            RECT rect;
            GetWindowRect((new WindowInteropHelper(window)).Handle, out rect);

            return rect;
        }

        public static RECT GetWindowRectangle(IntPtr hWnd)
        {
            RECT rect;
            GetWindowRect(hWnd, out rect);

            return rect;
        }

        public enum GWL
        {
            GWL_WNDPROC = (-4),
            GWL_HINSTANCE = (-6),
            GWL_HWNDPARENT = (-8),
            GWL_STYLE = (-16),
            GWL_EXSTYLE = (-20),
            GWL_USERDATA = (-21),
            GWL_ID = (-12)
        }

        public static void SetOnDesktop(Window window, bool activate, bool restart = false)
        {
            // Get Fuzion handle
            IntPtr hWnd = new WindowInteropHelper(window).Handle;

            if (activate)
            {
                //For compatibility reasons, SetParent does not modify the WS_CHILD or WS_POPUP window styles
                //of the window whose parent is being changed. Therefore, if hWndNewParent is NULL,
                //you should also clear the WS_CHILD bit and set the WS_POPUP style after calling SetParent.
                //Conversely, if hWndNewParent is not NULL and the window was previously a child of the desktop,
                //you should clear the WS_POPUP style and set the WS_CHILD style before calling SetParent.

                //When you change the parent of a window, you should synchronize the UISTATE of both windows.
                //For more information, see WM_CHANGEUISTATE and WM_UPDATEUISTATE.

                //Remove WS_POPUP style and add WS_CHILD style
                const uint WS_POPUP = 0x80000000;
                const uint WS_CHILD = 0x40000000;
                long style = (long)GetWindowLongPtr(hWnd, (int)GWL.GWL_STYLE);
                style = (style & ~WS_POPUP) | WS_CHILD;
                SetWindowLong(hWnd, (int)GWL.GWL_STYLE, (IntPtr)style);

                // Set parent to intermediate workerw
                SetParent(hWnd, IntermediateWorkerWPointer); //original
                //SetParent(hWnd, GetWorkerW());

                Console.WriteLine("Setting on Desktop");
                //AppWindow.Activate();

                if (restart)
                {
                    RestartFuzion();
                }
            }
            else
            {
                window.Owner = null;
                SetParent(hWnd, IntPtr.Zero); // intptr.zero will clear parent?

                const uint WS_POPUP = 0x80000000;
                const uint WS_CHILD = 0x40000000;
                long style = (long)GetWindowLongPtr(hWnd, (int)GWL.GWL_STYLE);
                //style = (style & ~WS_CHILD) | WS_CHILD;
                style = (style | WS_POPUP) & (~WS_CHILD);
                SetWindowLong(hWnd, (int)GWL.GWL_STYLE, (IntPtr)style);
            }

            //AppWindow.Activate();
            //MainWindow.CenterWindowOnScreen();
            _ = BringWindowToTop(hWnd);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd,uint Msg,IntPtr wParam,IntPtr lParam,SendMessageTimeoutFlags fuFlags,uint uTimeout,out IntPtr lpdwResult);

        [Flags]
        public enum SendMessageTimeoutFlags : uint
        {
            SMTO_NORMAL = 0x0,
            SMTO_BLOCK = 0x1,
            SMTO_ABORTIFHUNG = 0x2,
            SMTO_NOTIMEOUTIFNOTHUNG = 0x8,
            SMTO_ERRORONEXIT = 0x20
        }

        #region Spawn WorkerW
        /// <summary>
        /// Undocumented message for spawning a wallpaper worker on the program manager
        /// </summary>
        public static UInt32 WM_SPAWN_WORKER = 0x052C;
        public static UInt32 WM_CLOSE = 0x0010;

        // CREATE MY OWN WORKERW and use that for setondesktop
        /// <summary>
        /// https://github.com/Foohy/Wallpainter/blob/master/Wallpainter/WinAPI.cs - Check this on how to spawn a workerW
        /// </summary>
        /// <param name="progmanPtr"></param>
        private static void CreateIntermediateWorkerW(IntPtr progmanPtr)
        {
            IntPtr result = IntPtr.Zero;

            // Send 0x052C to Progman. This message directs Progman to spawn a 
            // WorkerW behind the desktop icons. If it is already there, nothing 
            // happens.
            SendMessageTimeout(progmanPtr,0x052C,new IntPtr(0),IntPtr.Zero,SendMessageTimeoutFlags.SMTO_NORMAL,1000,out result);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Retrieves the handle of the progman
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetProgman()
        {
            return FindWindow("Progman", null);
        }

        public static IntPtr GetWorkerW()
        {
            //get the handle of the progman window
            IntPtr progman = GetProgman();

            // get the handle of the SHELLDLL_DefView WorkerW top window
            // and create our WorkerW above it

            //Send the spooky undocumented message to the progman, which will spawn the new worker window
            // that is in charge of fading the wallpaper background
            SendMessage(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero);

            //This new worker window is a child of the SHELLDLL_DefView window, the default system shell window
            //Enumerate all the windows, looking for a "WorkerW" window that is an immediate sibling of "SHELLDLL_DefView"
            //And grab that window handle
            IntPtr workerw = IntPtr.Zero;
            EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
            {
                IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);

                //If we found that, look for the corresponding worker window as a sibling of that
                if (p != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
                }

                return true;
            }), IntPtr.Zero);

            ////Immediately hide the workerW window. Instead, we'll be parenting to the main progman 
            //ShowWindowAsync(workerw, 0);

            return workerw;
        }

        #endregion

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        //[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        //static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        private static IntPtr GetIntermediateWorkerW()
        {
            // Spy++ output
            // .....
            // 0x00010190 "" WorkerW
            //   ...
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            // 0x00100B8A "" WorkerW       <-- This is the WorkerW instance we are after!
            // 0x000100EC "Program Manager" Progman

            IntPtr workerw = IntPtr.Zero;

            // We enumerate all Windows, until we find one, that has the SHELLDLL_DefView 
            // as a child. 
            // If we found that window, we take its next sibling and assign it to workerw.
            _ = EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
              {
                  // Original
                  IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", string.Empty);
                  
                  //Console.WriteLine("Top Handle: "+tophandle);
                  //p = FindWindowEx(p, IntPtr.Zero, "SysListView32", string.Empty);

                  if (p != IntPtr.Zero)
                  {
                      Console.WriteLine("IntPtr is NONNULL");
                      IntPtr s = FindWindowEx(p, IntPtr.Zero, "SysListView32", string.Empty);
                      Console.WriteLine("SysListView32 handle: "+s);
                    // Take the folderview instead
                        //workerw = p; // original
                        workerw = tophandle;
                      //IntermediateWorkerWPointer = p;
                    //// Gets the WorkerW Window after the current one.
                    //workerw = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", string.Empty);
                }

                  return true;
              }), IntPtr.Zero);

            Console.WriteLine("Intermediate WorkerW Handle: "+workerw);
            return workerw;
        }



        //public static void SetOnDesktop(Window window, bool activate, bool restart = false)
        //{
        //    if (activate)
        //    {
        //        //var wih = new WindowInteropHelper(window);
        //        IntPtr hWnd = new WindowInteropHelper(window).Handle;
        //        //IntPtr hWndProgMan = FindWindow("Progman", "Program Manager");
        //        IntPtr hWndProgMan = FindWindow("WorkerW", "");
        //        // First set the owner then parent
        //        //wih.Owner = hWndProgMan;
        //        SetParent(hWnd, hWndProgMan); // intptr.zero will clear parent?
        //        //AppWindow.Topmost = true;
        //        NativeMethods.SetWindowPos()

        //        if (restart)
        //        {
        //            RestartFuzion();
        //        }

        //    } else
        //    {
        //        IntPtr hWnd = new WindowInteropHelper(window).Handle;
        //        window.Owner = null;
        //        SetParent(hWnd, IntPtr.Zero); // intptr.zero will clear parent?
                
        //    }

        //    //Console.WriteLine("Window State is: "+AppWindow.WindowState);
        //    //AppWindow.Show();
        //    //AppWindow.Topmost = true;

        //}

        static Shell32.ShellClass sh;
        public static bool minimizedAll;
        /// <summary>
        /// Add proper sh.UndoMinimizeAll detection
        /// </summary>
        public static void ShowDesktop()
        {
            if(sh == null)
            {
                Console.WriteLine("Shell was null attempting to show desktop, creating it");
                sh = new Shell32.ShellClass();
            }

            //sh.ToggleDesktop();
            sh.MinimizeAll();

            //if (minimizedAll)
            //{
            //    sh.UndoMinimizeALL();
            //    minimizedAll = false;
            //}
            //else
            //{
            //    sh.MinimizeAll();
            //    minimizedAll = true;
            //}

            //if (MainWindowActive)
            //{
            //    sh.UndoMinimizeALL();
            //}
            //else
            //{
            //    sh.MinimizeAll();
            //    AppWindow.Activate();
            //}

            //System.Threading.Thread.Sleep(100);
            //ForceSetForegroundWindow(AppWindow);

            //ForceSetForegroundWindow(IntermediateWorkerWPointer);
        }

        public static void ShowDesktopTest()
        {
            if (sh == null)
            {
                Console.WriteLine("Shell was null attempting to show desktop, creating it");
                sh = new Shell32.ShellClass();
            }

            //sh.ToggleDesktop();
            //sh.MinimizeAll();
            //sh.UndoMinimizeALL();
            sh.WindowSwitcher();

            //System.Threading.Thread.Sleep(100);
            //ForceSetForegroundWindow(AppWindow);
            AppWindow.Activate();
            //ForceSetForegroundWindow(IntermediateWorkerWPointer);

        }

        #region 64 Bit Check

        // is 64bit OS
        public static bool is64BitProcess = (IntPtr.Size == 8);
        public static bool is64BitOperatingSystem = is64BitProcess || InternalCheckIsWow64();

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

        public static bool InternalCheckIsWow64()
        {
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    if (!IsWow64Process(p.Handle, out retVal))
                    {
                        return false;
                    }
                    return retVal;
                }
            }
            else
            {
                return false;
            }
        }

        #endregion

        public static void HideFromTaskSwitcher()
        {
            WindowInteropHelper wndHelper = new WindowInteropHelper(AppWindow);
            int exStyle = (int)GetWindowLongPtr(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE);
            exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
        }

        [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
        public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

        /// <summary>
        /// Retrieves a handle to a window that has the specified relationship (Z-Order or owner) to the specified window.
        /// </summary>
        /// <remarks>The EnumChildWindows function is more reliable than calling GetWindow in a loop. An application that
        /// calls GetWindow to perform this task risks being caught in an infinite loop or referencing a handle to a window
        /// that has been destroyed.</remarks>
        /// <param name="hWnd">A handle to a window. The window handle retrieved is relative to this window, based on the
        /// value of the uCmd parameter.</param>
        /// <param name="uCmd">The relationship between the specified window and the window whose handle is to be
        /// retrieved.</param>
        /// <returns>
        /// If the function succeeds, the return value is a window handle. If no window exists with the specified relationship
        /// to the specified window, the return value is NULL. To get extended error information, call GetLastError.
        /// </returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, GetWindowType uCmd);

        private enum GetWindowType : uint
        {
            /// <summary>
            /// The retrieved handle identifies the window of the same type that is highest in the Z order.
            /// <para/>
            /// If the specified window is a topmost window, the handle identifies a topmost window.
            /// If the specified window is a top-level window, the handle identifies a top-level window.
            /// If the specified window is a child window, the handle identifies a sibling window.
            /// </summary>
            GW_HWNDFIRST = 0,
            /// <summary>
            /// The retrieved handle identifies the window of the same type that is lowest in the Z order.
            /// <para />
            /// If the specified window is a topmost window, the handle identifies a topmost window.
            /// If the specified window is a top-level window, the handle identifies a top-level window.
            /// If the specified window is a child window, the handle identifies a sibling window.
            /// </summary>
            GW_HWNDLAST = 1,
            /// <summary>
            /// The retrieved handle identifies the window below the specified window in the Z order.
            /// <para />
            /// If the specified window is a topmost window, the handle identifies a topmost window.
            /// If the specified window is a top-level window, the handle identifies a top-level window.
            /// If the specified window is a child window, the handle identifies a sibling window.
            /// </summary>
            GW_HWNDNEXT = 2,
            /// <summary>
            /// The retrieved handle identifies the window above the specified window in the Z order.
            /// <para />
            /// If the specified window is a topmost window, the handle identifies a topmost window.
            /// If the specified window is a top-level window, the handle identifies a top-level window.
            /// If the specified window is a child window, the handle identifies a sibling window.
            /// </summary>
            GW_HWNDPREV = 3,
            /// <summary>
            /// The retrieved handle identifies the specified window's owner window, if any.
            /// </summary>
            GW_OWNER = 4,
            /// <summary>
            /// The retrieved handle identifies the child window at the top of the Z order,
            /// if the specified window is a parent window; otherwise, the retrieved handle is NULL.
            /// The function examines only child windows of the specified window. It does not examine descendant windows.
            /// </summary>
            GW_CHILD = 5,
            /// <summary>
            /// The retrieved handle identifies the enabled popup window owned by the specified window (the
            /// search uses the first such window found using GW_HWNDNEXT); otherwise, if there are no enabled
            /// popup windows, the retrieved handle is that of the specified window.
            /// </summary>
            GW_ENABLEDPOPUP = 6
        }

        // needs translation to C#
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        //[DllImport("user32.dll", SetLastError = true)]
        //static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // When you don't want the ProcessId, use this overload and pass IntPtr.Zero for the second parameter
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        #region Sys Parameters P/invoke Info
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, ref uint pvParam, SPIF fWinIni); // ref uint can be replaced with any type such as IntPtr,etc.

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, uint pvParam, SPIF fWinIni);

        // For setting a string parameter
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, String pvParam, SPIF fWinIni);

        // For reading a string parameter
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, SPIF fWinIni);

        //[DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        //public static extern bool SystemParametersInfoGet(uint action, uint param, ref uint vparam, uint init);

        //[DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        //public static extern bool SystemParametersInfoSet(uint action, uint param, uint vparam, uint init);

        //[DllImport("user32.dll", SetLastError = true)]
        //[return: MarshalAs(UnmanagedType.Bool)]
        //static extern bool SystemParametersInfo(SPI uiAction, uint uiParam, ref ANIMATIONINFO pvParam, SPIF fWinIni);

        /// <summary>
        /// SPI_ System-wide parameter - Used in SystemParametersInfo function
        /// </summary>

        public enum SPI : uint
        {
            /// <summary>
            /// Retrieves the amount of time following user input, in milliseconds, during which the system will not allow applications
            /// to force themselves into the foreground. The pvParam parameter must point to a DWORD variable that receives the time.
            /// Windows NT, Windows 95:  This value is not supported.
            /// </summary>
            GETFOREGROUNDLOCKTIMEOUT = 0x2000,
            /// <summary>
            /// Sets the amount of time following user input, in milliseconds, during which the system does not allow applications
            /// to force themselves into the foreground. Set pvParam to the new timeout value.
            /// The calling thread must be able to change the foreground window, otherwise the call fails.
            /// Windows NT, Windows 95:  This value is not supported.
            /// </summary>
            SETFOREGROUNDLOCKTIMEOUT = 0x2001
        }

        [Flags]
        public enum SPIF
        {
            None = 0x00,
            /// <summary>Writes the new system-wide parameter setting to the user profile.</summary>
            UPDATEINIFILE = 0x01,
            /// <summary>Broadcasts the WM_SETTINGCHANGE message after updating the user profile.</summary>
            SENDCHANGE = 0x02,
            /// <summary>Same as SPIF_SENDCHANGE.</summary>
            SENDWININICHANGE = 0x02
        }

        #endregion

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);

        const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;

        private static void SetForegroundWindowInternal(IntPtr hWnd)
        {
            //// get current lock timeout value
            //uint timeout = 99;
            //bool retVal = SystemParametersInfoGet(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref timeout, 0);
            ////Console.WriteLine("GETFOREGROUNDLOCKTIMEOUT: "+retVal);

            ////System.Windows.Forms.MessageBox.Show("FOREGROUNDLOCKTIMEOUT IS "+timeout);

            //// set current lock timeout value to 0, so focus can be grabbed
            //SystemParametersInfoSet(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, 0, 0);


            if (!IsWindow(hWnd)) return;

            //relation time of SetForegroundWindow lock
            uint lockTimeOut = 0;
            var hCurrWnd = GetForegroundWindow();
            var dwThisTID = GetCurrentThreadId();
            var dwCurrTID = GetWindowThreadProcessId(hCurrWnd, IntPtr.Zero);

            //we need to bypass some limitations from Microsoft :)
            if (dwThisTID != dwCurrTID)
            {
                AttachThreadInput(dwThisTID, dwCurrTID, true);

                SystemParametersInfo(SPI.GETFOREGROUNDLOCKTIMEOUT, 0, ref lockTimeOut, 0);

                SystemParametersInfo(SPI.GETFOREGROUNDLOCKTIMEOUT, 0, 0, SPIF.SENDWININICHANGE | SPIF.UPDATEINIFILE);

                AllowSetForegroundWindow(ASFW_ANY);
            }

            SetForegroundWindow(hWnd);

            if (dwThisTID != dwCurrTID)
            {
                SystemParametersInfo(SPI.SETFOREGROUNDLOCKTIMEOUT, 0, /*(PVOID)*/lockTimeOut, SPIF.SENDWININICHANGE | SPIF.UPDATEINIFILE);

                AttachThreadInput(dwThisTID, dwCurrTID, false);
            }
        }

        /// <summary>
        /// Activates a WPF window even if the window is activated on a separate thread
        /// </summary>
        /// <param name="window"></param>
        public static void ActivateWindowForce(Window window)
        {
            var wih = new WindowInteropHelper(window);
            var hwnd = wih.EnsureHandle();

            var threadId1 = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
            var threadId2 = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

            if (threadId1 != threadId2)
            {
                AttachThreadInput(threadId1, threadId2, true);
                SetForegroundWindow(hwnd);
                AttachThreadInput(threadId1, threadId2, false);
            }
            else
                SetForegroundWindow(hwnd);
        }
        #region SHAppBarMessage
        public enum ABM : uint
        {
            New = 0x00000000,
            Remove = 0x00000001,
            QueryPos = 0x00000002,
            SetPos = 0x00000003,
            GetState = 0x00000004,
            GetTaskbarPos = 0x00000005,
            Activate = 0x00000006,
            GetAutoHideBar = 0x00000007,
            SetAutoHideBar = 0x00000008,
            WindowPosChanged = 0x00000009,
            SetState = 0x0000000A,
        }

        public enum ABE : uint
        {
            Left = 0,
            Top = 1,
            Right = 2,
            Bottom = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public ABE uEdge;
            public RECT rc;
            public int lParam;
        }


        [DllImport("shell32.dll", SetLastError = true)]
        public static extern IntPtr SHAppBarMessage(ABM dwMessage, [In] ref APPBARDATA pData);
        #endregion

        #region SHDRAGIMAGE
        //[ComImport]
        //[Guid("4657278A-411B-11d2-839A-00C04FD918D0")]
        //public class DragDropHelper { }

        [StructLayout(LayoutKind.Sequential)]
        public struct Win32Point
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Win32Size
        {
            public int cx;
            public int cy;
        }

        [ComVisible(true)]
        [ComImport, System.Runtime.InteropServices.Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDragSourceHelper
        {
            void InitializeFromBitmap(
                [In, MarshalAs(UnmanagedType.Struct)] ref ShDragImage dragImage,
                [In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject);

            void InitializeFromWindow(
                [In] IntPtr hwnd,
                [In] ref Win32Point pt,
                [In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ShDragImage
        {
            public Win32Size sizeDragImage;
            public Win32Point ptOffset;
            public IntPtr hbmpDragImage;
            public int crColorKey;
        }

        [ComVisible(true)]
        [ComImport, System.Runtime.InteropServices.Guid("4657278B-411B-11D2-839A-00C04FD918D0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDropTargetHelper
        {
            void DragEnter(
                [In] IntPtr hwndTarget,
                [In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
                [In] ref Win32Point pt,
                [In] int effect);

            void DragLeave();

            void DragOver(
                [In] ref Win32Point pt,
                [In] int effect);

            void Drop(
                [In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
                [In] ref Win32Point pt,
                [In] int effect);

            void Show(
                [In] bool show);
        }
        #endregion

        #region Mouse

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
        // DON'T use System.Drawing.Point, the order of the fields in System.Drawing.Point isn't guaranteed to stay the same.

        public static Point GetMousePosPinvoke()
        {
            GetCursorPos(out POINT cursorPos);
            return new Point((double)cursorPos.x, (double)cursorPos.y);
        }

        public static Point GetWPFMousePositionWithinWindow()
        {
            var pt = Mouse.GetPosition(AppWindow);
            var res = new Point((double)pt.X, (double)pt.Y);
            return res;
        }
        #endregion
    }
}
