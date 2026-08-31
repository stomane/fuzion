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
            base.Write(value);
            textbox.Dispatcher.BeginInvoke(new Action(() =>
              {
                  textbox.AppendText(value.ToString(formatProvider));
              }));
        }

        public override void Write(string value)
        {
            textbox.Text += value;
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
            //System.Windows.Forms.MessageBox.Show(e.ExceptionObject.ToString());
            //ExceptionReporting.Report(e.ExceptionObject.ToString());
            Native.ThreadedHook.DisableAllHooks();
            // Crash reporting to Sentry is intentionally not wired up yet - usage tracking
            // (Release Health sessions) is opt-out-by-default, but actual crash/error capture
            // should only start once it's exposed as an explicit user opt-in setting.
            DebugWindow error = new DebugWindow();
            error.DebugTextBox.Text = e.ExceptionObject.ToString();
            error.Title = "Exception Stacktrace";
            error.ShowDialog();
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
