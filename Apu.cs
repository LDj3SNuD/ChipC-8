using System;
using System.Diagnostics;

namespace ChipC_8
{
    public enum SoundState { Off, On }

    public class Apu
    {
        public bool Enabled { get; set; }
        public int MinTimerValueForSoundOn { get; }

        public SoundState SoundState { get; private set; }

        private readonly Cpu _cpu;
        private readonly Gpu _gpu;

        public Apu(Cpu cpu, Gpu gpu, bool enabled, int minTimerValueForSoundOn)
        {
            Trace.Assert(minTimerValueForSoundOn > 0 && minTimerValueForSoundOn <= 255);

            Enabled = enabled;
            MinTimerValueForSoundOn = minTimerValueForSoundOn;

            _cpu = cpu;
            _gpu = gpu;
        }

        public void SoundStateUpdate(ConsoleColor altFColor = Program.DefaultBackColor, ConsoleColor altBColor = Program.DefaultForeColor, bool force = false)
        {
            if (!force)
            {
                int value = _cpu.ReadTimer(TimerType.Sound);

                if (Enabled && value >= MinTimerValueForSoundOn)
                {
                    if (SoundState == SoundState.Off)
                    {
                        SoundState = SoundState.On;

                        _gpu.PrintInit(altBorders: true, altFColor: altFColor, altBColor: altBColor);
                    }
                }
                else if (value == 0)
                {
                    if (SoundState == SoundState.On)
                    {
                        SoundState = SoundState.Off;

                        _gpu.PrintInit();
                    }
                }
            }
            else
            {
                if (SoundState == SoundState.On)
                {
                    _gpu.PrintInit(altBorders: true, altFColor: altFColor, altBColor: altBColor);
                }
                else /* if (SoundState == SoundState.Off) */
                {
                    _gpu.PrintInit();
                }
            }
        }
    }
}