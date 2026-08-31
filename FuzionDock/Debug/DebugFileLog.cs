#if DEBUG
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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

            // Most of this codebase logs with Console.WriteLine, which in a WinExe with no
            // console attached goes nowhere - not to the log file, and not to the debugger.
            // Routing Console.Out into Trace makes every existing call site show up in both
            // debug.log and the IDE's debug output, without touching hundreds of call sites.
            Console.SetOut(new TraceTextWriter());

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Trace.Flush();
        }

        /// <summary>
        /// Forwards writes into Trace a line at a time. Console.WriteLine can arrive as a series
        /// of single Write(char) calls, so characters are buffered until a newline rather than
        /// emitting one trace entry per character. Console.SetOut wraps this in a synchronized
        /// writer, so the buffer doesn't need its own locking.
        /// </summary>
        private sealed class TraceTextWriter : TextWriter
        {
            private readonly StringBuilder buffer = new StringBuilder();

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    Flush();
                }
                else if (value != '\r')
                {
                    buffer.Append(value);
                }
            }

            public override void Write(string value)
            {
                if (value == null)
                {
                    return;
                }

                foreach (char c in value)
                {
                    Write(c);
                }
            }

            public override void WriteLine(string value)
            {
                if (buffer.Length > 0)
                {
                    Trace.WriteLine(buffer.ToString() + value);
                    buffer.Clear();
                }
                else
                {
                    Trace.WriteLine(value);
                }
            }

            public override void Flush()
            {
                if (buffer.Length > 0)
                {
                    Trace.WriteLine(buffer.ToString());
                    buffer.Clear();
                }
            }
        }
    }
}
#endif
