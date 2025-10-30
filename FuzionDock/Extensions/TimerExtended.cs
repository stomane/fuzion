using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Fuzion.Extensions
{
    internal class TimerExtended : IDisposable
    {
        public bool IsRunning { get; private set; }

        private readonly Timer timer;
        private readonly TimerCallback _callback;
        private readonly bool _once;
        private readonly int _dueTime;
        private readonly int _period;

        public TimerExtended(TimerCallback callback, object state, int dueTime, int period, bool once = false, bool startPaused = false)
        {
            _callback = callback;
            _once = once;
            _dueTime = dueTime;
            _period = period;
            

            if (startPaused)
            {
                timer = new Timer(TimerTick, state, Timeout.Infinite, Timeout.Infinite);
                IsRunning = false;
            }
            else
            {
                timer = new Timer(TimerTick, state, dueTime, period);
                IsRunning = true;
            }

        }

        private void TimerTick(object state)
        {
            _callback.Invoke(state);

            if (_once)
            {
                Dispose();
            }
        }

        public void Start()
        {
            if (!IsRunning)
            {
                _ = timer.Change(_dueTime, _period);
                IsRunning = true;
            }
        }

        public void Pause()
        {
            if (IsRunning)
            {
                _ = timer.Change(Timeout.Infinite, Timeout.Infinite);
                IsRunning = false;
            }
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
