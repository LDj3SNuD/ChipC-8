using System;
using System.Runtime.InteropServices;

namespace ChipC_8
{
    [Flags]
    public enum KeyStates
    {
        None = 0,
        Down = 1,
        Toggled = 2
    }

    public static class KeypadDevice
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /**/

        public static KeyStates GetKeyStates(ConsoleKey cKey)
        {
            IntPtr consoleWindowHandle = GetConsoleWindow();

            if (consoleWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(nameof(consoleWindowHandle));
            }

            KeyStates keyStates = KeyStates.None;

            if (GetForegroundWindow() == consoleWindowHandle)
            {
                short keyState = GetAsyncKeyState((int)cKey);

                if (((keyState >> 15) & 1) == 1)
                {
                    keyStates |= KeyStates.Down;
                }

                if ((keyState & 1) == 1)
                {
                    keyStates |= KeyStates.Toggled;
                }
            }

            return keyStates;
        }

        public static bool IsKeyDown(ConsoleKey cKey)
        {
            switch (cKey)
            {
                case ConsoleKey.W: return GetKeyStates(ConsoleKey.W).HasFlag(KeyStates.Down) || GetKeyStates(ConsoleKey.UpArrow).HasFlag(KeyStates.Down);
                case ConsoleKey.A: return GetKeyStates(ConsoleKey.A).HasFlag(KeyStates.Down) || GetKeyStates(ConsoleKey.LeftArrow).HasFlag(KeyStates.Down);
                case ConsoleKey.S: return GetKeyStates(ConsoleKey.S).HasFlag(KeyStates.Down) || GetKeyStates(ConsoleKey.DownArrow).HasFlag(KeyStates.Down);
                case ConsoleKey.D: return GetKeyStates(ConsoleKey.D).HasFlag(KeyStates.Down) || GetKeyStates(ConsoleKey.RightArrow).HasFlag(KeyStates.Down);

                default: return GetKeyStates(cKey).HasFlag(KeyStates.Down);
            }
        }
    }
}