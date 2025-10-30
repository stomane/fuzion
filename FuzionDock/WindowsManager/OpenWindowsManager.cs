using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.WindowsManager
{
    public static class OpenWindowsManager
    {
        public static List<string> Tags { get; } = new List<string> { };
        public static List<object> Windows { get; } = new List<object> { };

        public enum R { Add, Remove }
        /// <summary>
        /// Check if a window is already open.
        /// </summary>
        /// <param name="windowTag">The previously set window tag</param>
        /// <returns>True if window is open</returns>
        public static bool IsWindowOpen(string windowTag)
        {
            if (Tags.Contains(windowTag))
            {
                return true;
            } else
            {
                return false;
            }
        }

        public static void WindowReferenceControl(string windowTag, object windowObject ,R addRemove)
        {
            if (addRemove == R.Add)
            {
                Tags.Add(windowTag);
                Windows.Add(windowObject);
            } else //remove
            {
                Tags.Remove(windowTag);
                Windows.Remove(windowObject);
            }
        }
    }
}
