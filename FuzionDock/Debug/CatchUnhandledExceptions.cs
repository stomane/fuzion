using Fuzion.SQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Fuzion.Debug
{
    // Output to a custom console
    internal class MultiTextWriter : TextWriter
    {
        private readonly IEnumerable<TextWriter> writers;
        public MultiTextWriter(IEnumerable<TextWriter> writers)
        {
            this.writers = writers.ToList();
        }
        public MultiTextWriter(params TextWriter[] writers)
        {
            this.writers = writers;
        }

        public override void Write(char value)
        {
            foreach (var writer in writers)
                writer.Write(value);
        }

        public override void Write(string value)
        {
            foreach (var writer in writers)
                writer.Write(value);
        }

        public override void Flush()
        {
            foreach (var writer in writers)
                writer.Flush();
        }

        public override void Close()
        {
            foreach (var writer in writers)
                writer.Close();
        }

        public override Encoding Encoding
        {
            // was ASCII
            get { return Encoding.ASCII; }
        }
    }

    public class ControlWriter : TextWriter
    {
        // No idea why I had to add this
        readonly IFormatProvider formatProvider;

        private readonly TextBox textbox;
        public ControlWriter(TextBox textbox)
        {
            this.textbox = textbox;
        }

        public override void Write(char value)
        {
            Write(value.ToString(formatProvider));
        }

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            // Console output arrives from background threads. `textbox.Text +=` from off the
            // UI thread throws, and the old per-character BeginInvoke queued one dispatcher
            // operation per character - enough traffic to starve the UI thread on a chatty
            // scan. Marshal once per write instead, and never block the caller.
            textbox.Dispatcher.BeginInvoke(new Action(() => textbox.AppendText(value)));
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }

    class CatchUnhandledExceptions
    {
        private static void MessageBoxOn_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Native.ThreadedHook.DisableAllHooks();

            // Crash reporting to Sentry is intentionally not wired up yet - usage tracking
            // (Release Health sessions) is opt-out-by-default, but actual crash/error capture
            // should only start once it's exposed as an explicit user opt-in setting.
            Console.WriteLine("Unhandled exception: " + e.ExceptionObject);

#if DEBUG
            // Debug only. A modal stack-trace window is useful at the desk, but in a shipped
            // build it parks the process behind a dialog with nobody there to dismiss it -
            // and ShowDialog off a background thread throws on top of the original fault,
            // so the user would see a hang rather than the crash.
            try
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(new Action(() =>
                {
                    DebugWindow error = new DebugWindow();
                    error.DebugTextBox.Text = e.ExceptionObject.ToString();
                    error.Title = "Exception Stacktrace";
                    error.ShowDialog();
                }));
            }
            catch (Exception)
            {
                // The process is already going down - never fault inside the fault handler.
            }
#endif
        }

        public static void EnableMessageBoxOnUnhandledException(bool enable)
        {
            try
            {
                if (enable)
                    AppDomain.CurrentDomain.UnhandledException += MessageBoxOn_UnhandledException;
                else
                    AppDomain.CurrentDomain.UnhandledException -= MessageBoxOn_UnhandledException;
            }
            catch (Exception)
            {

            }           
        }
    }
}
