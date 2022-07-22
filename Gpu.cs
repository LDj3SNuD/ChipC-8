using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChipC_8
{
    public enum SChipMode { LowRes, HighRes }

    public enum ScrollDirection { Down, Up, Right, Left }

    public enum SpriteMode { Clip, Wrap }

    public class Gpu
    {
        public CompatibilityMode CompatibilityMode { get; }
        public SChipMode SChipMode { get; private set; }

        public SpriteMode SpriteMode { get; }

        public int FrameBufferWidth { get; }
        public int FrameBufferHeight { get; }

        private readonly bool[,] _frameBuffer;
        private readonly List<string> _rows;

        public Gpu(CompatibilityMode compatibilityMode, SpriteMode spriteMode)
        {
            CompatibilityMode = compatibilityMode;
            SChipMode = SChipMode.LowRes;

            SpriteMode = spriteMode;

            if (compatibilityMode == CompatibilityMode.Vip)
            {
                FrameBufferWidth = 64;
                FrameBufferHeight = 32;
            }
            else /* if (compatibilityMode == CompatibilityMode.SChip) */
            {
                FrameBufferWidth = 128;
                FrameBufferHeight = 64;
            }

            _frameBuffer = new bool [FrameBufferHeight, FrameBufferWidth];
            _rows = new(FrameBufferHeight);
        }

        public void PrintInit(int left = 1, int top = 5, bool altBorders = false, ConsoleColor altFColor = Program.DefaultBackColor, ConsoleColor altBColor = Program.DefaultForeColor)
        {
            int width = 1 + FrameBufferWidth + 1;
            int height = 1 + FrameBufferHeight + 1;

            Console.ForegroundColor = !altBorders ? ConsoleColor.DarkGray : altFColor;
            Console.BackgroundColor = !altBorders ? ConsoleColor.Black : altBColor;

            Program.Write("┌" + new String('─', width - 2) + "┐", left, top);

            for (int i = 1; i <= height - 2; i++)
            {
                Program.Write("│", left, top + i);
                Program.Write("│", left + width - 1, top + i);
            }

            Program.Write("└" + new String('─', width - 2) + "┘", left, top + height - 1);

            Console.ResetColor();
        }

        public void PrintUpdate(int left = 2, int top = 6, ConsoleColor fColor = Program.DefaultForeColor, ConsoleColor bColor = Program.DefaultBackColor)
        {
            _rows.Clear();

            if (CompatibilityMode == CompatibilityMode.SChip && SChipMode == SChipMode.LowRes)
            {
                for (int y = 0; y < FrameBufferHeight / 2; y++)
                {
                    string row = String.Empty;

                    for (int x = 0; x < FrameBufferWidth / 2; x++)
                    {
                        row += _frameBuffer[y, x] ? "██" : "  ";
                    }

                    _rows.Add(row);
                    _rows.Add(row);
                }
            }
            else /* if (CompatibilityMode == CompatibilityMode.Vip || SChipMode == SChipMode.HighRes) */
            {
                for (int y = 0; y < FrameBufferHeight; y++)
                {
                    string str = String.Empty;

                    for (int x = 0; x < FrameBufferWidth; x++)
                    {
                        str += _frameBuffer[y, x] ? "█" : " ";
                    }

                    _rows.Add(str);
                }
            }

            Console.ForegroundColor = fColor;
            Console.BackgroundColor = bColor;

            for (int y = 0; y < FrameBufferHeight; y++)
            {
                Program.Write(_rows[y], left, top + y);
            }

            Console.ResetColor();
        }

        /**/

        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_display.md
        public void SetSChipMode(SChipMode sChipMode)
        {
            Clear();

            SChipMode = sChipMode;
        }

        public void Scroll(ScrollDirection scrollDirection, int amount)
        {
            Trace.Assert(amount >= 1 && amount <= 15);

            switch (scrollDirection)
            {
                case ScrollDirection.Down:
                {
                    for (int i = 1; i <= amount; i++)
                    {
                        for (int y = FrameBufferHeight - 1; y >= 1; y--)
                        {
                            for (int x = 0; x < FrameBufferWidth; x++)
                            {
                                _frameBuffer[y, x] = _frameBuffer[y - 1, x];
                            }
                        }

                        for (int x = 0; x < FrameBufferWidth; x++)
                        {
                            _frameBuffer[0, x] = false;
                        }
                    }

                    break;
                }

                case ScrollDirection.Up:
                {
                    for (int i = 1; i <= amount; i++)
                    {
                        for (int y = 0; y < FrameBufferHeight - 1; y++)
                        {
                            for (int x = 0; x < FrameBufferWidth; x++)
                            {
                                _frameBuffer[y, x] = _frameBuffer[y + 1, x];
                            }
                        }
                    }

                    for (int x = 0; x < FrameBufferWidth; x++)
                    {
                        _frameBuffer[FrameBufferHeight - 1, x] = false;
                    }

                    break;
                }

                case ScrollDirection.Right:
                {
                    for (int i = 1; i <= amount; i++)
                    {
                        for (int x = FrameBufferWidth - 1; x >= 1; x--)
                        {
                            for (int y = 0; y < FrameBufferHeight; y++)
                            {
                                _frameBuffer[y, x] = _frameBuffer[y, x - 1];
                            }
                        }

                        for (int y = 0; y < FrameBufferHeight; y++)
                        {
                            _frameBuffer[y, 0] = false;
                        }
                    }

                    break;
                }

                case ScrollDirection.Left:
                {
                    for (int i = 1; i <= amount; i++)
                    {
                        for (int x = 0; x < FrameBufferWidth - 1; x++)
                        {
                            for (int y = 0; y < FrameBufferHeight; y++)
                            {
                                _frameBuffer[y, x] = _frameBuffer[y, x + 1];
                            }
                        }

                        for (int y = 0; y < FrameBufferHeight; y++)
                        {
                            _frameBuffer[y, FrameBufferWidth - 1] = false;
                        }
                    }

                    break;
                }
            }
        }

        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_16x.md
        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_collide.md
        public void DrawSprite(int x, int y, int[] spriteData, int bitsPerRow, out bool isCollision)
        {
            Trace.Assert(spriteData.Length >= 1 && spriteData.Length <= 16);
            Trace.Assert(bitsPerRow == 8 || bitsPerRow == 16);

            int frameBufferWidth = FrameBufferWidth;
            int frameBufferHeight = FrameBufferHeight;

            if (CompatibilityMode == CompatibilityMode.SChip && SChipMode == SChipMode.LowRes)
            {
                frameBufferWidth /= 2;
                frameBufferHeight /= 2;
            }

            x %= frameBufferWidth;
            y %= frameBufferHeight;

            isCollision = false;

            for (int yOffset = 0; yOffset < spriteData.Length; yOffset++)
            {
                for (int xOffset = 0; xOffset <= (bitsPerRow - 1); xOffset++)
                {
                    if (((spriteData[yOffset] >> ((bitsPerRow - 1) - xOffset)) & 1) == 1)
                    {
                        int xDst = x + xOffset;
                        int yDst = y + yOffset;

                        if (SpriteMode == SpriteMode.Clip)
                        {
                            if (xDst >= frameBufferWidth || yDst >= frameBufferHeight)
                            {
                                continue;
                            }
                        }
                        else /* if (SpriteMode == SpriteMode.Wrap) */
                        {
                            xDst %= frameBufferWidth;
                            yDst %= frameBufferHeight;
                        }

                        if (_frameBuffer[yDst, xDst])
                        {
                            _frameBuffer[yDst, xDst] = false;

                            isCollision = true;
                        }
                        else
                        {
                            _frameBuffer[yDst, xDst] = true;
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            int frameBufferWidth = FrameBufferWidth;
            int frameBufferHeight = FrameBufferHeight;

            if (CompatibilityMode == CompatibilityMode.SChip && SChipMode == SChipMode.LowRes)
            {
                frameBufferWidth /= 2;
                frameBufferHeight /= 2;
            }

            for (int y = 0; y < frameBufferHeight; y++)
            {
                for (int x = 0; x < frameBufferWidth; x++)
                {
                    _frameBuffer[y, x] = false;
                }
            }
        }
    }
}