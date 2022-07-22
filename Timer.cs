using System;
using System.Diagnostics;

namespace ChipC_8
{
    public enum TimerType { Delay, Sound }

    public class Timer
    {
        private int _startValue;

        private long _clockStartValueUS;

        private readonly Clock _clock;

        public Timer(Clock clock)
        {
            _clock = clock;
        }

        public int GetValue()
        {
            if (_startValue == 0)
            {
                return 0;
            }

            Trace.Assert(_clockStartValueUS != 0);

            long clockDeltaValueUS = _clock.ValueUS - _clockStartValueUS;

            Trace.Assert(clockDeltaValueUS >= 0);

            int endValue = Math.Max(0, _startValue - (int)(clockDeltaValueUS / _clock.PeriodUS));

            Trace.Assert(endValue <= _startValue);

            if (endValue == 0)
            {
                _startValue = 0;

                _clockStartValueUS = 0;
            }

            return endValue;
        }

        public void SetValue(int value)
        {
            _startValue = value;

            _clockStartValueUS = value != 0 ? _clock.ValueUS : 0;
        }
    }
}