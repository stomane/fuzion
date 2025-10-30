using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Fuzion.Native.NativeMethods;

namespace Fuzion.Native
{
    class TaskbarSpecific
    {
        public enum TaskbarPosition
        {
            Unknown = -1,
            Left,
            Top,
            Right,
            Bottom,
        }

        public sealed class Taskbar
        {
            private const string ClassName = "Shell_TrayWnd";

            public Rectangle Bounds
            {
                get;
                private set;
            }
            public TaskbarPosition Position
            {
                get;
                private set;
            }
            public Point Location
            {
                get
                {
                    return this.Bounds.Location;
                }
            }
            public Size Size
            {
                get
                {
                    return this.Bounds.Size;
                }
            }
            //Always returns false under Windows 7
            public bool AlwaysOnTop
            {
                get;
                private set;
            }

            public bool AutoHide
            {
                get;
                private set;
            }

            public Taskbar()
            {
                IntPtr taskbarHandle = NativeMethods.FindWindow(Taskbar.ClassName, null);

                APPBARDATA data = new APPBARDATA();
                data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                data.hWnd = taskbarHandle;
                IntPtr result = NativeMethods.SHAppBarMessage(ABM.GetTaskbarPos, ref data);
                if (result == IntPtr.Zero)
                    throw new InvalidOperationException();

                this.Position = (TaskbarPosition)data.uEdge;
                this.Bounds = Rectangle.FromLTRB(data.rc.Left, data.rc.Top, data.rc.Right, data.rc.Bottom);

                data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
                result = NativeMethods.SHAppBarMessage(ABM.GetState, ref data);
                int state = result.ToInt32();
                this.AlwaysOnTop = (state & ABS.AlwaysOnTop) == ABS.AlwaysOnTop;
                this.AutoHide = (state & ABS.Autohide) == ABS.Autohide;
            }
        }

        public static class ABS
        {
            public const int Autohide = 0x0000001;
            public const int AlwaysOnTop = 0x0000002;
        }

    }
}
