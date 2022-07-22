// https://www.pinvoke.net/default.aspx/kernel32/SetCurrentConsoleFontEx.html

using System;
using System.Runtime.InteropServices;

namespace ChipC_8
{
    public static class FontDevice
    {
        [DllImport("kernel32.dll")]
        private static extern bool SetCurrentConsoleFontEx(IntPtr consoleOutput, bool maximumWindow, ref ConsoleFontInfo consoleCurrentFont);

        [DllImport("kernel32")]
        private static extern IntPtr GetStdHandle(StdHandle stdHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ConsoleFontInfo
        {
            public uint Size;
            public uint FontIndex;
            public Coord FontSize;
            public int FontFamily; // tmPitchAndFamily @ TEXTMETRIC
            public int FontWeight;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Coord
        {
            public short X;
            public short Y;
        };

        private enum StdHandle
        {
            Output = -11
        }

        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        /**/

        public static bool TrySetCurrentConsoleFont(string faceName, int fontSizeX, int fontSizeY)
        {
            if (String.IsNullOrEmpty(faceName))
            {
                return false;
            }

            IntPtr stdHandle = GetStdHandle(StdHandle.Output);

            if (stdHandle == InvalidHandleValue)
            {
                return false;
            }

            ConsoleFontInfo consoleFontInfo = new();
            consoleFontInfo.Size = (uint)Marshal.SizeOf(consoleFontInfo);

            consoleFontInfo.FontSize.X = (short)fontSizeX;
            consoleFontInfo.FontSize.Y = (short)fontSizeY;

            consoleFontInfo.FaceName = faceName;

            return SetCurrentConsoleFontEx(stdHandle, maximumWindow: false, ref consoleFontInfo);
        }
    }
}