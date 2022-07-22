using System.Diagnostics;

namespace ChipC_8
{
    public class Clock
    {
        public long Freq { get; }
        public long PeriodUS => 1000000 / Freq;

        public long ValueUS => (_clock.ElapsedTicks * 1000000) / Stopwatch.Frequency;

        private readonly Stopwatch _clock;

        public Clock(int freq)
        {
            Freq = freq;

            _clock = new();
        }

        public void Start()
        {
            _clock.Start();
        }

        public void Stop()
        {
            _clock.Stop();
        }

        public void StopAndReset()
        {
            _clock.Reset();
        }

        public void Sync()
        {
            if (_clock.IsRunning)
            {
                long deltaValueUS = PeriodUS - (ValueUS % PeriodUS);

                Program.Wait(deltaValueUS, precise: true);
            }
            else
            {
                Program.Wait(PeriodUS, precise: false);
            }
        }
    }
}