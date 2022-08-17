using System;
using System.Collections.Generic;

namespace ChipC_8
{
    public class Keypad
    {
        public const int Width = 45;
        public const int Height = 19;

        public List<ConsoleKey> Bindings { get; }

        public bool Waiting { get; set; }

        private readonly List<int> _keys;
        private readonly List<int> _keyHints;

        private readonly List<(bool pressed, bool hinted)> _printCache;
        private bool _waitingCache;

        private bool _oneKeyJustPressedState;

        public Keypad()
        {
            Bindings = new();

            Bindings.Add(ConsoleKey.X);  // 0
            Bindings.Add(ConsoleKey.D1); // 1
            Bindings.Add(ConsoleKey.D2); // 2
            Bindings.Add(ConsoleKey.D3); // 3
            Bindings.Add(ConsoleKey.Q);  // 4
            Bindings.Add(ConsoleKey.W);  // 5
            Bindings.Add(ConsoleKey.E);  // 6
            Bindings.Add(ConsoleKey.A);  // 7
            Bindings.Add(ConsoleKey.S);  // 8
            Bindings.Add(ConsoleKey.D);  // 9
            Bindings.Add(ConsoleKey.Z);  // A
            Bindings.Add(ConsoleKey.C);  // B
            Bindings.Add(ConsoleKey.D4); // C
            Bindings.Add(ConsoleKey.R);  // D
            Bindings.Add(ConsoleKey.F);  // E
            Bindings.Add(ConsoleKey.V);  // F

            _keys = new();
            _keyHints = new();

            _printCache = new();
        }

        public void Reset(bool full = true)
        {
            Waiting = false;

            _oneKeyJustPressedState = false;

            if (full)
            {
                _keys.Clear();
                _keyHints.Clear();
            }
        }

        public void Update()
        {
            _keys.Clear();

            for (int key = 0x0; key <= 0xF; key++)
            {
                if (KeypadDevice.IsKeyDown(Bindings[key]))
                {
                    _keys.Add(key);
                }
            }
        }

        public void PrintInit()
        {
            int left = (Console.WindowWidth - Width) / 2;
            int top = Console.WindowHeight - Height - 1;

            Program.Write("          ┌───────────────────────┐          ", left, top,      ConsoleColor.DarkGray);
            Program.Write("          │                       │          ", left, top + 1,  ConsoleColor.DarkGray);
            Program.Write("          ├─────┬─────┬─────┬─────┤          ", left, top + 2,  ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 3,  ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 4,  ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 5,  ConsoleColor.DarkGray);
            Program.Write("          ├─────┼─────┼─────┼─────┤          ", left, top + 6,  ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 7,  ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 8,  ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 9,  ConsoleColor.DarkGray);
            Program.Write("          ├─────┼─────┼─────┼─────┤          ", left, top + 10, ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 11, ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 12, ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 13, ConsoleColor.DarkGray);
            Program.Write("          ├─────┼─────┼─────┼─────┤          ", left, top + 14, ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 15, ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 16, ConsoleColor.DarkGray);
            Program.Write("          │     │     │     │     │          ", left, top + 17, ConsoleColor.DarkGray);
            Program.Write("          └─────┴─────┴─────┴─────┘          ", left, top + 18, ConsoleColor.DarkGray);

            _waitingCache = false;

            _printCache.Clear();

            for (int key = 0x0; key <= 0xF; key++)
            {
                bool pressed = false;
                bool hinted = false;

                _printCache.Add((pressed, hinted));

                PrintKey(key, pressed, hinted);
            }
        }

        public void PrintUpdate()
        {
            if (Waiting != _waitingCache)
            {
                _waitingCache = Waiting;

                string str = Waiting ? "Waiting for a key press" : new String(' ', 23);

                int left = (Console.WindowWidth - str.Length) / 2;
                int top = Console.WindowHeight - Height;

                Program.Write(str, left, top, ConsoleColor.White);
            }

            for (int key = 0x0; key <= 0xF; key++)
            {
                bool pressed = _keys.Contains(key);
                bool hinted = _keyHints.Contains(key);

                if ((pressed, hinted) != _printCache[key])
                {
                    _printCache[key] = (pressed, hinted);

                    PrintKey(key, pressed, hinted);
                }
            }
        }

        private void PrintKey(int key, bool pressed = false, bool hinted = false)
        {
            int left = (Console.WindowWidth - Width) / 2 + 10;
            int top = Console.WindowHeight - Height + 1;

            char GetBindingChar(int key) => $"{Bindings[key]}"[^1];

            (string str, int leftOffset, int topOffset) = key switch
            {
                0x0 => ($" {GetBindingChar(key)}→0 ", 7,  14),
                0x1 => ($" {GetBindingChar(key)}→1 ", 1,  2),
                0x2 => ($" {GetBindingChar(key)}→2 ", 7,  2),
                0x3 => ($" {GetBindingChar(key)}→3 ", 13, 2),
                0x4 => ($" {GetBindingChar(key)}→4 ", 1,  6),
                0x5 => ($" {GetBindingChar(key)}→5 ", 7,  6),
                0x6 => ($" {GetBindingChar(key)}→6 ", 13, 6),
                0x7 => ($" {GetBindingChar(key)}→7 ", 1,  10),
                0x8 => ($" {GetBindingChar(key)}→8 ", 7,  10),
                0x9 => ($" {GetBindingChar(key)}→9 ", 13, 10),
                0xA => ($" {GetBindingChar(key)}→A ", 1,  14),
                0xB => ($" {GetBindingChar(key)}→B ", 13, 14),
                0xC => ($" {GetBindingChar(key)}→C ", 19, 2),
                0xD => ($" {GetBindingChar(key)}→D ", 19, 6),
                0xE => ($" {GetBindingChar(key)}→E ", 19, 10),
                0xF => ($" {GetBindingChar(key)}→F ", 19, 14),
                _ => default
            };

            (ConsoleColor fColor, ConsoleColor bColor) = (pressed, hinted) switch
            {
                (false, false) => (ConsoleColor.DarkGray, ConsoleColor.Black),
                (false, true)  => (ConsoleColor.White,    ConsoleColor.Black),
                (true,  false) => (ConsoleColor.Black,    ConsoleColor.DarkGray),
                (true,  true)  => (ConsoleColor.Black,    ConsoleColor.White)
            };

            Program.Write("     ", left + leftOffset, top + topOffset - 1, fColor, bColor);
            Program.Write(str,     left + leftOffset, top + topOffset,     fColor, bColor);
            Program.Write("     ", left + leftOffset, top + topOffset + 1, fColor, bColor);
        }

        /**/

        public bool IsKeyValid(int key)
        {
            return key >= 0x0 && key <= 0xF;
        }

        public bool IsKeyPressed(int key)
        {
            return _keys.Contains(key);
        }

        public bool IsOneKeyJustPressed(out int key)
        {
            if (_oneKeyJustPressedState && _keys.Count == 1)
            {
                _oneKeyJustPressedState = false;

                key = _keys[0];

                return true;
            }

            _oneKeyJustPressedState = _keys.Count == 0;

            key = default;

            return false;
        }

        public void AddKeyHint(int key)
        {
            if (!_keyHints.Contains(key))
            {
                _keyHints.Add(key);
            }
        }
    }
}