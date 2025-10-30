using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using SharpDX.XInput;
using Fuzion.Extensions;

namespace Fuzion.Gamepad
{
    static class Repeater
    {
        public static State ControllerState { get; set; }
        public static TimerExtended InitialDelayTimer { get; } = new TimerExtended(InitialDelayTimer_Tick, null, 750, 0, false, true);
        public static TimerExtended RepeaterTimer { get; } = new TimerExtended(RepeaterTimer_Tick, null, 40, 40, false, true);

        private static void InitialDelayTimer_Tick(object s)
        {
            Console.WriteLine("Initial Delay Timer Tick");
            Console.WriteLine("Starting Repeater Timer");
            RepeaterTimer.Start();
            InitialDelayTimer.Pause();
        }

        private static void RepeaterTimer_Tick(object s)
        {
            //Application.Current.Dispatcher.Invoke(() => {  });
            Bindings.leftAnalogReady = true;
            Bindings.NavigateFuzion(ControllerState);
            Console.WriteLine("Repeater Timer Tick");
        }

        public static void ToggleInitialDelay(bool enable)
        {
            Console.WriteLine("Toggling Initial Delay "+ enable);

            if (enable)
            {
                Console.WriteLine("Starting Initial Delay Timer");
                InitialDelayTimer.Start();
            }
            else
            {
                InitialDelayTimer.Pause();
                RepeaterTimer.Pause();
            }
        }
    }
}
