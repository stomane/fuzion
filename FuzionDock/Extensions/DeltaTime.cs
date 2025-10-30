using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Fuzion.Extensions
{

    public static class DeltaTime
    {
        public static double GetSeconds { get; private set; }
        public static double GetMilliseconds { get; private set; }
        public static long GetTicks { get; private set; }

        public static void Initialize()
        {
            Rendering += DeltaTime_Rendering;
        }

        private static TimeSpan prev = TimeSpan.Zero;

        private static void DeltaTime_Rendering(object sender, RenderingEventArgs e)
        {
            //Console.WriteLine("Prev "+prev);
            //Console.WriteLine("Current "+e.RenderingTime);
            GetMilliseconds = e.RenderingTime.TotalMilliseconds - prev.TotalMilliseconds;
            GetSeconds = e.RenderingTime.TotalSeconds - prev.TotalSeconds;
            GetTicks = e.RenderingTime.Ticks - prev.Ticks;
            prev = e.RenderingTime;
        }

        private static TimeSpan _last = TimeSpan.Zero;
        private static event EventHandler<RenderingEventArgs> FrameUpdating;
        public static event EventHandler<RenderingEventArgs> Rendering
        {
            add
            {
                if (FrameUpdating == null)
                    CompositionTarget.Rendering += CompositionTarget_Rendering;
                FrameUpdating += value;
            }
            remove
            {
                FrameUpdating -= value;
                if (FrameUpdating == null)
                    CompositionTarget.Rendering -= CompositionTarget_Rendering;
            }
        }

        private static void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            RenderingEventArgs args = (RenderingEventArgs)e;
            if (args.RenderingTime == _last)
                return;
            _last = args.RenderingTime;
            FrameUpdating(sender, args);
        }
    }
}
