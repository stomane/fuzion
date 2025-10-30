using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fuzion.Debug
{
    static class DebugConsole
    {
        public static void OpenDebugWindow()
        {
            DebugWindow debugWindow = new DebugWindow();
            debugWindow.Show();
            ControlWriter cw = new ControlWriter(debugWindow.DebugTextBox); //were enabled
            Console.SetOut(cw);
            cw.Dispose();
        }
    }
}
