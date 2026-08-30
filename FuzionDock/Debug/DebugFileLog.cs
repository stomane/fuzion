#if DEBUG
using System;
using System.Diagnostics;
using System.IO;

namespace Fuzion.Debug
{
    internal static class DebugFileLog
    {
        public static string LogPath { get; private set; }

        public static void Initialize()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fuzion", "Logs");
            Directory.CreateDirectory(dir);
            LogPath = Path.Combine(dir, "debug.log");

            var writer = new StreamWriter(LogPath, append: false) { AutoFlush = true };
            Trace.Listeners.Add(new TextWriterTraceListener(writer));
            Trace.AutoFlush = true;

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Trace.Flush();
        }
    }
}
#endif
