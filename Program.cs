using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ChipC_8
{
    public class Program
    {
        public const int DefaultSpeed = 20; // cycles/frame.

        public const ConsoleColor DefaultForeColor = ConsoleColor.Yellow;
        public const ConsoleColor DefaultBackColor = ConsoleColor.DarkYellow;

        [STAThread]
        public static void Main(string[] args)
        {
            if (!FontDevice.TrySetCurrentConsoleFont(faceName: "Terminal", fontSizeX: 8, fontSizeY: 8)) // Font: Raster (Terminal 8x8).
            {
                throw new Exception("Failed to set current console font.");
            }

            Console.Title = "ChipC-8";

            Console.OutputEncoding = System.Text.Encoding.Unicode;

            Console.CursorVisible = false;

            //Console.TreatControlCAsInput = true;

            /**/

            if (!TrySetWindowAndBufferSize(width: 64, height: 32))
            {
                throw new Exception("Failed to set window and buffer size.");
            }

            Console.Clear();
            Console.ResetColor();

            if (args.Length != 1)
            {
                string str = "Usage: ChipC-8 <rom file>";
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 - 1, ConsoleColor.White);

                str = "[Press Escape to Exit]";
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 + 2, ConsoleColor.DarkGray);
                while (Console.ReadKey(true).Key != ConsoleKey.Escape);

                return;
            }

            /**/

            string fileName = Path.GetFileName(args[0]);

            CompatibilityMode compatibilityMode = CompatibilityMode.Vip;

            bool done = false;
            do
            {
                string str = $"Load {fileName} as {CompatibilityMode.Vip} Rom";
                ConsoleColor fColor = compatibilityMode == CompatibilityMode.Vip ? ConsoleColor.Black : ConsoleColor.Gray;
                ConsoleColor bColor = compatibilityMode == CompatibilityMode.Vip ? ConsoleColor.White : ConsoleColor.Black;
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 - 3, fColor, bColor);

                str = $"Load {fileName} as {CompatibilityMode.SChip} Rom";
                fColor = compatibilityMode == CompatibilityMode.SChip ? ConsoleColor.Black : ConsoleColor.Gray;
                bColor = compatibilityMode == CompatibilityMode.SChip ? ConsoleColor.White : ConsoleColor.Black;
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 - 1, fColor, bColor);

                str = $"[Press ↑ / ↓ to Select the Compatibility Mode]";
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 + 2, ConsoleColor.DarkGray);
                str = $"[Press Enter to Confirm or Escape to Exit]";
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 + 3, ConsoleColor.DarkGray);

                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.UpArrow:   compatibilityMode = CompatibilityMode.Vip;   break;
                    case ConsoleKey.DownArrow: compatibilityMode = CompatibilityMode.SChip; break;
                    case ConsoleKey.Enter: done = true; break;

                    case ConsoleKey.Escape: return;
                };
            }
            while (!done);

            /**/

            Clock clock = new(freq: 60);

            Gpu gpu = new(compatibilityMode, spriteMode: SpriteMode.Clip);
            Keypad keypad = new();
            Cpu cpu = new(compatibilityMode, clock, gpu, keypad);
            Apu apu = new(cpu, gpu, enabled: true, minTimerValueForSoundOn: 3);

            int speed = DefaultSpeed;

            ConsoleColor foreColor = DefaultForeColor;
            ConsoleColor backColor = DefaultBackColor;

            bool pause = false;

            bool exitHost = false;
            bool exitGuest = false;

            /**/

            byte[] romData = File.ReadAllBytes(args[0]);

            if (!cpu.TryLoadRom(romData, out int loadedBytes, out int freeBytes))
            {
                Console.Clear();
                Console.ResetColor();

                string str = $"Cannot Load {fileName} as {compatibilityMode} Rom";
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 - 1, ConsoleColor.White);

                str = "[Press Escape to Exit]";
                Write(str, (Console.WindowWidth - str.Length) / 2, Console.WindowHeight / 2 + 2, ConsoleColor.DarkGray);
                while (Console.ReadKey(true).Key != ConsoleKey.Escape);

                return;
            }

            /**/

            bool firstPause = false;

            void PrintInfo(int top = 1, bool init = false)
            {
                if (init)
                {
                    string str0 = $"[{fileName}] [Loaded bytes: {loadedBytes} (Free bytes: {freeBytes})] [{compatibilityMode}]";

                    int left0 = (Console.WindowWidth - str0.Length) / 2;

                    Program.Write(str0, left0, top, ConsoleColor.DarkGray);
                }

                if (pause) firstPause = true;

                string str = $"[Speed: {speed} cycs/frm]{(!apu.Enabled ? " [NO SOUND]" : String.Empty)}";
                str += $"{(pause ? " [PAUSE]" : (!firstPause ? " - Press P for Pause & Help" : String.Empty))}";

                int pad = (Console.WindowWidth - str.Length) / 2;

                Program.Write(new String(' ', pad) + str + new String(' ', pad), left: 0, top + 2, ConsoleColor.Gray);
            }

            void Pause()
            {
                if (!pause)
                {
                    clock.Stop();

                    pause = true;

                    PrintInfo();

                    keypad.Reset(full: false);

                    PrintControls();
                }
            }

            void Resume()
            {
                if (pause)
                {
                    pause = false;

                    PrintInfo();

                    keypad.PrintInit();

                    clock.Start();
                }
            }

            /**/

            if (!TrySetWindowAndBufferSize(2 + gpu.FrameBufferWidth + 2, 6 + gpu.FrameBufferHeight + 2 + Keypad.Height + 1))
            {
                throw new Exception("Failed to set window and buffer size.");
            }

            Console.Clear();
            Console.ResetColor();

            PrintInfo(init: true);

            gpu.PrintInit();

            keypad.PrintInit();

            /**/

            clock.Start();

            clock.Sync();

            while (true)
            {
                if (!pause)
                {
                    keypad.Update();

                    int cnt = speed;
                    do
                    {
                        cpu.Execute(out bool needsSync, out exitGuest);

                        cnt--;

                        if (needsSync || exitGuest)
                        {
                            cnt = 0;
                        }
                    }
                    while (cnt != 0);

                    apu.SoundStateUpdate(altFColor: backColor, altBColor: foreColor);
                    gpu.PrintUpdate(fColor: foreColor, bColor: backColor);

                    keypad.PrintUpdate();
                }

                clock.Sync();

                if (exitGuest)
                {
                    Pause();
                }

                /**/

                if (Console.KeyAvailable)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;

                    switch (key)
                    {   
                        // Inc/Dec Speed.
                        case ConsoleKey.Add:
                        case ConsoleKey.Subtract:
                        {
                            if (!pause) clock.Stop();

                            speed = Math.Clamp(speed + (key == ConsoleKey.Add ? 1 : -1), 1, 100);

                            PrintInfo();

                            if (!pause) clock.Start();

                            break;
                        }

                        // Enable/Disable Sound.
                        case ConsoleKey.Backspace:
                        {
                            if (!pause) clock.Stop();

                            apu.Enabled = !apu.Enabled;

                            PrintInfo();

                            if (!pause) clock.Start();

                            break;
                        }

                        // Pause.
                        case ConsoleKey.P when !exitGuest:
                        {
                            Pause();

                            break;
                        }

                        // Resume.
                        case ConsoleKey.Enter when !exitGuest:
                        {
                            Resume();

                            break;
                        }

                        // Reset.
                        case ConsoleKey.Delete:
                        {
                            if (pause)
                            {
                                pause = false;

                                PrintInfo();

                                keypad.PrintInit();

                                if (!exitGuest)
                                {
                                    foreColor = DefaultForeColor;
                                    backColor = DefaultBackColor;
                                }
                            }

                            clock.StopAndReset();

                            cpu.Reset(romData);

                            apu.SoundStateUpdate(altFColor: backColor, altBColor: foreColor);

                            gpu.SetSChipMode(SChipMode.LowRes);
                            gpu.PrintUpdate(fColor: foreColor, bColor: backColor);

                            keypad.Reset();
                            keypad.PrintUpdate();

                            clock.Start();

                            clock.Sync();

                            break;
                        }

                        // Next/Prev Primary Color.
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.DownArrow:
                        {
                            if (pause)
                            {
                                do
                                {
                                    foreColor += (key == ConsoleKey.UpArrow ? 1 : -1);

                                    if (foreColor > ConsoleColor.White)
                                    {
                                        foreColor = ConsoleColor.Black;
                                    }
                                    else if (foreColor < ConsoleColor.Black)
                                    {
                                        foreColor = ConsoleColor.White;
                                    }
                                }
                                while (foreColor == backColor);

                                apu.SoundStateUpdate(altFColor: backColor, altBColor: foreColor, force: true);
                                gpu.PrintUpdate(fColor: foreColor, bColor: backColor);
                            }

                            break;
                        }

                        // Next/Prev Secondary Color.
                        case ConsoleKey.RightArrow:
                        case ConsoleKey.LeftArrow:
                        {
                            if (pause)
                            {
                                do
                                {
                                    backColor += (key == ConsoleKey.RightArrow ? 1 : -1);

                                    if (backColor > ConsoleColor.White)
                                    {
                                        backColor = ConsoleColor.Black;
                                    }
                                    else if (backColor < ConsoleColor.Black)
                                    {
                                        backColor = ConsoleColor.White;
                                    }
                                }
                                while (backColor == foreColor);

                                apu.SoundStateUpdate(altFColor: backColor, altBColor: foreColor, force: true);
                                gpu.PrintUpdate(fColor: foreColor, bColor: backColor);
                            }

                            break;
                        }

                        // Swap Primary/Secondary Colors.
                        case ConsoleKey.I:
                        {
                            if (pause)
                            {
                                ConsoleColor color = foreColor;

                                foreColor = backColor;
                                backColor = color;

                                apu.SoundStateUpdate(altFColor: backColor, altBColor: foreColor, force: true);
                                gpu.PrintUpdate(fColor: foreColor, bColor: backColor);
                            }

                            break;
                        }

                        // Exit.
                        case ConsoleKey.Escape:
                        {
                            exitHost = true;

                            break;
                        }
                    }
                }

                if (exitHost)
                {
                    break;
                }
            }
        }

        private static bool TrySetWindowAndBufferSize(int width, int height)
        {
            if (Console.LargestWindowWidth < width || Console.LargestWindowHeight < height)
            {
                return false;
            }

            Console.SetWindowSize(width, height);

            if (Console.WindowLeft != 0 || Console.WindowTop != 0)
            {
                return false;
            }

            Console.SetBufferSize(width, height); // BufferWidth >= WindowLeft + WindowWidth // BufferHeight >= WindowTop + WindowHeight

            //Console.SetWindowPosition(0, 0); // WindowLeft + WindowWidth <= BufferWidth // WindowTop + WindowHeight <= BufferHeight

            return true;
        }

        public static void Write(string str, int left, int top, ConsoleColor fColor = ConsoleColor.Gray, ConsoleColor bColor = ConsoleColor.Black)
        {
            if (fColor != ConsoleColor.Gray)  Console.ForegroundColor = fColor;
            if (bColor != ConsoleColor.Black) Console.BackgroundColor = bColor;

            Write(str, left, top);

            Console.ResetColor();
        }

        public static void Write(string str, int left, int top)
        {
            Console.SetCursorPosition(left, top);
            Console.Write(str);
        }

        private static readonly Stopwatch _wait = new();

        public static void Wait(long valueUS, bool precise)
        {
            if (precise)
            {
                _wait.Restart();

                while ((_wait.ElapsedTicks * 1000000) / Stopwatch.Frequency < valueUS);
            }
            else
            {
                Thread.Sleep((int)(valueUS / 1000));
            }
        }

        private static void PrintControls()
        {
            const int Width = 45;
            const int Height = 19;

            int left = (Console.WindowWidth - Width) / 2;
            int top = Console.WindowHeight - Height - 1;

            Write("┌──────────┬────────────────────────────────┐", left, top,      ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 1,  ConsoleColor.DarkGray);
            Write("├──────────┼────────────────────────────────┤", left, top + 2,  ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 3,  ConsoleColor.DarkGray);
            Write("├──────────┼────────────────────────────────┤", left, top + 4,  ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 5,  ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 6,  ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 7,  ConsoleColor.DarkGray);
            Write("├──────────┼────────────────────────────────┤", left, top + 8,  ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 9,  ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 10, ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 11, ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 12, ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 13, ConsoleColor.DarkGray);
            Write("├──────────┼────────────────────────────────┤", left, top + 14, ConsoleColor.DarkGray);
            Write("│          │                                │", left, top + 15, ConsoleColor.DarkGray);
            Write("└──────────┴────────────────────────────────┘", left, top + 16, ConsoleColor.DarkGray);
            Write("                                             ", left, top + 17, ConsoleColor.DarkGray);
            Write("                                             ", left, top + 18, ConsoleColor.DarkGray);

            Write("+ / -    ", left + 1, top + 1,  ConsoleColor.White);
            Write("Backspace", left + 1, top + 3,  ConsoleColor.White);
            Write("P        ", left + 1, top + 5,  ConsoleColor.White);
            Write("Enter    ", left + 1, top + 6,  ConsoleColor.White);
            Write("Delete   ", left + 1, top + 7,  ConsoleColor.White);
            Write("↑ / ↓    ", left + 1, top + 10, ConsoleColor.White);
            Write("→ / ←    ", left + 1, top + 11, ConsoleColor.White);
            Write("I        ", left + 1, top + 12, ConsoleColor.White);
            Write("Escape   ", left + 1, top + 15, ConsoleColor.White);

            Write("Inc / Dec Speed                ", left + 12, top + 1,  ConsoleColor.Gray);
            Write("Enable / Disable Sound         ", left + 12, top + 3,  ConsoleColor.Gray);
            Write("Pause                          ", left + 12, top + 5,  ConsoleColor.Gray);
            Write("Resume                         ", left + 12, top + 6,  ConsoleColor.Gray);
            Write("Reset                          ", left + 12, top + 7,  ConsoleColor.Gray);
            Write("While on Pause:                ", left + 12, top + 9,  ConsoleColor.Gray);
            Write("Next / Prev Primary Color      ", left + 12, top + 10, ConsoleColor.Gray);
            Write("Next / Prev Secondary Color    ", left + 12, top + 11, ConsoleColor.Gray);
            Write("Swap Primary / Secondary Colors", left + 12, top + 12, ConsoleColor.Gray);
            Write("(Reset also resets the Colors) ", left + 12, top + 13, ConsoleColor.Gray);
            Write("Exit                           ", left + 12, top + 15, ConsoleColor.Gray);
        }
    }
}