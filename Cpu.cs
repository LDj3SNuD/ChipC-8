using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChipC_8
{
    public enum CompatibilityMode { Vip, SChip }

    public class Cpu
    {
        public CompatibilityMode CompatibilityMode { get; }

        public int DataStart { get; }

        public int FontDataStart { get; }
        public int HighFontDataStart { get; }

        public int CodeDataStart { get; }
        public int CodeDataEnd { get; private set; }

        public int CodeDataEndMax { get; }

        public int Sp => _stack.Count;

        /**/

        private readonly int[] _mem; // n x 8 bit.

        private int _pc; // 12 bit.
        private int _i; // 12 bit.

        private readonly int[] _v; // 16 x 8 bit.
        private readonly int[] _rpl; // 8 x 8 bit.

        private readonly Stack<int> _stack; // n x 12 bit.

        private readonly Timer _dt; // 8 bit.
        private readonly Timer _st; // 8 bit.

        private readonly Random _rnd;

        /**/

        private readonly Gpu _gpu;
        private readonly Keypad _keypad;

        public Cpu(CompatibilityMode compatibilityMode, Clock clock, Gpu gpu, Keypad keypad)
        {
            CompatibilityMode = compatibilityMode;

            DataStart = 0x0;

            // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_memlimit.md
            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                CodeDataStart = 0x200;
                CodeDataEndMax = 0xEA0 - 1;
            }
            else /* if (compatibilityMode == CompatibilityMode.SChip) */
            {
                CodeDataStart = 0x200;
                CodeDataEndMax = 0xFFF - 1;
            }

            _mem = new int[CodeDataEndMax + 1];

            int[] fontData = new int[] // Vip.
            {
                0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
                0x20, 0x60, 0x20, 0x20, 0x70, // 1
                0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
                0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
                0x90, 0x90, 0xF0, 0x10, 0x10, // 4
                0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
                0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
                0xF0, 0x10, 0x20, 0x40, 0x40, // 7
                0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
                0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
                0xF0, 0x90, 0xF0, 0x90, 0x90, // A
                0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
                0xF0, 0x80, 0x80, 0x80, 0xF0, // C
                0xE0, 0x90, 0x90, 0x90, 0xE0, // D
                0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
                0xF0, 0x80, 0xF0, 0x80, 0x80  // F
            };

            FontDataStart = DataStart;

            Array.Copy(fontData, 0, _mem, FontDataStart, fontData.Length);

            int[] highFontData = new int[] // SChip.
            {
		        0x3C, 0x7E, 0xE7, 0xC3, 0xC3, 0xC3, 0xC3, 0xE7, 0x7E, 0x3C, // 0
		        0x18, 0x38, 0x58, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, // 1
		        0x3E, 0x7F, 0xC3, 0x06, 0x0C, 0x18, 0x30, 0x60, 0xFF, 0xFF, // 2
		        0x3C, 0x7E, 0xC3, 0x03, 0x0E, 0x0E, 0x03, 0xC3, 0x7E, 0x3C, // 3
		        0x06, 0x0E, 0x1E, 0x36, 0x66, 0xC6, 0xFF, 0xFF, 0x06, 0x06, // 4
		        0xFF, 0xFF, 0xC0, 0xC0, 0xFC, 0xFE, 0x03, 0xC3, 0x7E, 0x3C, // 5
		        0x3E, 0x7C, 0xE0, 0xC0, 0xFC, 0xFE, 0xC3, 0xC3, 0x7E, 0x3C, // 6
		        0xFF, 0xFF, 0x03, 0x06, 0x0C, 0x18, 0x30, 0x60, 0x60, 0x60, // 7
		        0x3C, 0x7E, 0xC3, 0xC3, 0x7E, 0x7E, 0xC3, 0xC3, 0x7E, 0x3C, // 8
		        0x3C, 0x7E, 0xC3, 0xC3, 0x7F, 0x3F, 0x03, 0x03, 0x3E, 0x7C  // 9
            };

            HighFontDataStart = fontData.Length;

            Array.Copy(highFontData, 0, _mem, HighFontDataStart, highFontData.Length);

            _pc = CodeDataStart - 2;

            _v = new int[16];
            _rpl = new int[8];

            _stack = new();

            _dt = new(clock);
            _st = new(clock);

            _rnd = new(/*Seed: 0*/);

            _gpu = gpu;
            _keypad = keypad;
        }

        public bool TryLoadRom(byte[] romData, out int loadedBytes, out int freeBytes)
        {
            loadedBytes = 0;
            freeBytes = 0;

            if (romData.Length == 0 || CodeDataStart + romData.Length - 1 > CodeDataEndMax)
            {
                return false;
            }

            CodeDataEnd = CodeDataStart + romData.Length - 1;

            loadedBytes = CodeDataEnd - CodeDataStart + 1;
            freeBytes = CodeDataEndMax - CodeDataEnd;

            Array.Copy(romData, 0, _mem, CodeDataStart, romData.Length);

            return true;
        }

        public void Reset(byte[] romData)
        {
            Array.Copy(romData, 0, _mem, CodeDataStart, romData.Length);

            Array.Clear(_mem, CodeDataEnd + 1, CodeDataEndMax - CodeDataEnd);

            _pc = CodeDataStart - 2;
            _i = 0;

            Array.Clear(_v, 0, 16);

            _stack.Clear();

            _dt.SetValue(0);
            _st.SetValue(0);
        }

        public void Execute(out bool needsSync, out bool exitGuest)
        {
            needsSync = false;
            exitGuest = false;

            IncPc();

            /*
            nnn or addr - A 12-bit value, the lowest 12 bits of the instruction
            n or nibble - A 4-bit value, the lowest 4 bits of the instruction
            x - A 4-bit value, the lower 4 bits of the high byte of the instruction
            y - A 4-bit value, the upper 4 bits of the low byte of the instruction
            kk or byte - An 8-bit value, the lowest 8 bits of the instruction
            */

            //                                 x           y           n           kk           nnn
            FetchAndDecode(out int n3, out int n2, out int n1, out int n0, out int n10, out int n210);

            switch ((n3, n2, n1, n0))
            {
                // Chip-8 Instructions.
                case (0x0, 0x0, 0xE, 0x0): Clear(out needsSync);          break; // 00E0 - CLS
                case (0x0, 0x0, 0xE, 0xE): Ret();                         break; // 00EE - RET
              //case (0x0,   _,   _,   _): CallSys(n210);                 break; // 0nnn - SYS addr
                case (0x1,   _,   _,   _): JumpU(n210);                   break; // 1nnn - JP addr
                case (0x2,   _,   _,   _): Call(n210);                    break; // 2nnn - CALL addr
                case (0x3,   _,   _,   _): SkipIfEqualRegImm(n2, n10);    break; // 3xkk - SE Vx, byte
                case (0x4,   _,   _,   _): SkipIfNotEqualRegImm(n2, n10); break; // 4xkk - SNE Vx, byte
                case (0x5,   _,   _, 0x0): SkipIfEqualRegReg(n2, n1);     break; // 5xy0 - SE Vx, Vy
                case (0x6,   _,   _,   _): LoadRegImm(n2, n10);           break; // 6xkk - LD Vx, byte
                case (0x7,   _,   _,   _): AddRegImm(n2, n10);            break; // 7xkk - ADD Vx, byte
                case (0x8,   _,   _, 0x0): LoadRegReg(n2, n1);            break; // 8xy0 - LD Vx, Vy
                case (0x8,   _,   _, 0x1): OrRegReg(n2, n1);              break; // 8xy1 - OR Vx, Vy
                case (0x8,   _,   _, 0x2): AndRegReg(n2, n1);             break; // 8xy2 - AND Vx, Vy
                case (0x8,   _,   _, 0x3): XorRegReg(n2, n1);             break; // 8xy3 - XOR Vx, Vy
                case (0x8,   _,   _, 0x4): AddRegReg(n2, n1);             break; // 8xy4 - ADD Vx, Vy
                case (0x8,   _,   _, 0x5): SubRegReg(n2, n1);             break; // 8xy5 - SUB Vx, Vy
                case (0x8,   _,   _, 0x6): ShrRegReg(n2, n1);             break; // 8xy6 - SHR Vx {, Vy}
                case (0x8,   _,   _, 0x7): SubNegRegReg(n2, n1);          break; // 8xy7 - SUBN Vx, Vy
                case (0x8,   _,   _, 0xE): ShlRegReg(n2, n1);             break; // 8xyE - SHL Vx {, Vy}
                case (0x9,   _,   _, 0x0): SkipIfNotEqualRegReg(n2, n1);  break; // 9xy0 - SNE Vx, Vy
                case (0xA,   _,   _,   _): SetI(n210);                    break; // Annn - LD I, addr
                case (0xB,   _,   _,   _): JumpPlusU(n210);               break; // Bnnn - JP V0, addr
                case (0xC,   _,   _,   _): RandRegImm(n2, n10);           break; // Cxkk - RND Vx, byte
                case (0xD,   _,   _,   _): DrawSprite(n2, n1, n0, out needsSync); break; // Dxyn - DRW Vx, Vy, nibble
                case (0xE,   _, 0x9, 0xE): SkipIfKey(n2);                 break; // Ex9E - SKP Vx
                case (0xE,   _, 0xA, 0x1): SkipIfNotKey(n2);              break; // ExA1 - SKNP Vx
                case (0xF,   _, 0x0, 0x7): GetDelayTimer(n2);             break; // Fx07 - LD Vx, DT
                case (0xF,   _, 0x0, 0xA): WaitAnyKey(n2, out needsSync); break; // Fx0A - LD Vx, K
                case (0xF,   _, 0x1, 0x5): SetDelayTimer(n2);             break; // Fx15 - LD DT, Vx
                case (0xF,   _, 0x1, 0x8): SetSoundTimer(n2);             break; // Fx18 - LD ST, Vx
                case (0xF,   _, 0x1, 0xE): AddI(n2);                      break; // Fx1E - ADD I, Vx
                case (0xF,   _, 0x2, 0x9): LoadFont(n2);                  break; // Fx29 - LD F, Vx
                case (0xF,   _, 0x3, 0x3): Bcd(n2);                       break; // Fx33 - LD B, Vx
                case (0xF,   _, 0x5, 0x5): Store(n2);                     break; // Fx55 - LD [I], Vx
                case (0xF,   _, 0x6, 0x5): Load(n2);                      break; // Fx65 - LD Vx, [I]

                // Super Chip-48 Instructions.
                case (0x0, 0x0, 0xC,   _): ScrollDown(n0, out needsSync); break; // 00Cn - SCD nibble
                case (0x0, 0x0, 0xF, 0xB): ScrollRight(out needsSync);    break; // 00FB - SCR
                case (0x0, 0x0, 0xF, 0xC): ScrollLeft(out needsSync);     break; // 00FC - SCL
                case (0x0, 0x0, 0xF, 0xD): Exit(out exitGuest);           break; // 00FD - EXIT
                case (0x0, 0x0, 0xF, 0xE): LowRes(out needsSync);         break; // 00FE - LOW
                case (0x0, 0x0, 0xF, 0xF): HighRes(out needsSync);        break; // 00FF - HIGH
              //case (0xD,   _,   _, 0x0): DrawSprite(n2, n1, n: 0, out needsSync); break; // Dxy0 - DRW Vx, Vy, 0
                case (0xF,   _, 0x3, 0x0): LoadHighFont(n2);              break; // Fx30 - LD HF, Vx
                case (0xF,   _, 0x7, 0x5): StoreRpl(n2);                  break; // Fx75 - LD R, Vx
                case (0xF,   _, 0x8, 0x5): LoadRpl(n2);                   break; // Fx85 - LD Vx, R

                default: throw new NotImplementedException($"Not Implemented Instruction: 0x{n3:X1}{n2:X1}{n1:X1}{n0:X1}.");
            }
        }

        private void FetchAndDecode(out int n3, out int n2, out int n1, out int n0, out int n10, out int n210)
        {
            int n32 = ReadMemory(_pc);
            n10 = ReadMemory(_pc + 1);

            n3 = (n32 >> 4) & 0b1111;
            n2 = n32 & 0b1111;

            n1 = (n10 >> 4) & 0b1111;
            n0 = n10 & 0b1111;

            n210 = n2 << 8 | n1 << 4 | n0;
        }

        /* Chip-8 Instructions. */

        // 00E0 | CLS | CHIP-8 | Clears the display. Sets all pixels to off.
        private void Clear(out bool needsSync)
        {
            needsSync = false;

            _gpu.Clear();

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                needsSync = true;
            }
        }

        // 00EE | RET | CHIP-8 | Return from subroutine. Set the PC to the address at the top of the stack and subtract 1 from the SP.
        private void Ret()
        {
            WritePc(_stack.Pop());
        }

        // 0NNN | CALL NNN | CHIP-8 | Call machine language subroutine at address NNN.
        //private void CallSys(int nnn) { }

        // 1NNN | JMP NNN | CHIP-8 | Set PC to NNN.
        private void JumpU(int nnn)
        {
            WritePc(nnn - 2);
        }

        // 2NNN | CALL NNN | CHIP-8 | Call subroutine a NNN. Increment the SP and put the current PC value on the top of the stack. Then set the PC to NNN.
        private void Call(int nnn)
        {
            Trace.Assert(_stack.Count < (CompatibilityMode == CompatibilityMode.Vip ? 12 : 16));

            _stack.Push(_pc);

            WritePc(nnn - 2);
        }

        // 3XNN | SE VX, NN | CHIP-8 | Skip the next instruction if register VX is equal to NN.
        private void SkipIfEqualRegImm(int xIdx, int nn)
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(nn >= 0 && nn <= 255);

            if (vX == nn)
            {
                IncPc();
            }
        }

        // 4XNN | SNE VX, NN | CHIP-8 | Skip the next instruction if register VX is not equal to NN.
        private void SkipIfNotEqualRegImm(int xIdx, int nn)
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(nn >= 0 && nn <= 255);

            if (vX != nn)
            {
                IncPc();
            }
        }

        // 5XY0 | SE VX, VY | CHIP-8 | Skip the next instruction if register VX equals VY.
        private void SkipIfEqualRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            if (vX == vY)
            {
                IncPc();
            }
        }

        // 6XNN | LD VX, NN | CHIP-8 | Load immediate value NN into register VX.
        private void LoadRegImm(int xIdx, int nn)
        {
            WriteRegister(xIdx, nn);
        }

        // 7XNN | ADD VX, NN | CHIP-8 | Add immediate value NN to register VX. Does not effect VF.
        private void AddRegImm(int xIdx, int nn)
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(nn >= 0 && nn <= 255);

            vX += nn;

            WriteRegister(xIdx, vX & 255);
        }

        // 8XY0 | LD VX, VY | CHIP-8 | Copy the value in register VY into VX.
        private void LoadRegReg(int xIdx, int yIdx)
        {
            int vY = ReadRegister(yIdx);

            WriteRegister(xIdx, vY);
        }

        // 8XY1 | OR VX, VY | CHIP-8 | Set VX equal to the bitwise or of the values in VX and VY.
        private void OrRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            vX |= vY;

            WriteRegister(xIdx, vX);

            // Octo: quirks.logic: if 1, clear vf after &=,|= and ^=. On the VIP, these instructions leave vf in an unknown state.
            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                WriteRegister(index: 15, value: 0);
            }
        }

        // 8XY2 | AND VX, VY | CHIP-8 | Set VX equal to the bitwise and of the values in VX and VY.
        private void AndRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            vX &= vY;

            WriteRegister(xIdx, vX);

            // Octo: quirks.logic: if 1, clear vf after &=,|= and ^=. On the VIP, these instructions leave vf in an unknown state.
            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                WriteRegister(index: 15, value: 0);
            }
        }

        // 8XY3 | XOR VX, VY | CHIP-8 | Set VX equal to the bitwise xor of the values in VX and VY.
        private void XorRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            vX ^= vY;

            WriteRegister(xIdx, vX);

            // Octo: quirks.logic: if 1, clear vf after &=,|= and ^=. On the VIP, these instructions leave vf in an unknown state.
            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                WriteRegister(index: 15, value: 0);
            }
        }

        // 8XY4 | ADD VX, VY | CHIP-8 | Set VX equal to VX plus VY. In the case of an overflow VF is set to 1. Otherwise 0.
        private void AddRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            vX += vY;

            WriteRegister(xIdx, vX & 255);

            WriteRegister(index: 15, vX > 255 ? 1 : 0);
        }

        // 8XY5 | SUB VX, VY | CHIP-8 | Set VX equal to VX minus VY. In the case of an underflow VF is set 0. Otherwise 1.
        private void SubRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            vX = vX - vY;

            WriteRegister(xIdx, vX & 255);

            WriteRegister(index: 15, vX < 0 ? 0 : 1);
        }

        // 8XY6 | SHR VX, VY | CHIP-8 | Set VX equal to VY bitshifted right 1. VF is set to the least significant bit of VX prior to the shift.
        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_shift.md
        private void ShrRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            WriteRegister(index: 15, vX & 1);

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                vX = vY >> 1;
            }
            else /* if (CompatibilityMode == CompatibilityMode.SChip) */
            {
                vX = vX >> 1;
            }

            WriteRegister(xIdx, vX);
        }

        // 8XY7 | SUBN VX, VY | CHIP-8 | Set VX equal to VY minus VX. VF is set to 1 if VY > VX. Otherwise 0.
        private void SubNegRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            vX = vY - vX;

            WriteRegister(xIdx, vX & 255);

            WriteRegister(index: 15, vX < 0 ? 0 : 1);
        }

        // 8XYE | SHL VX, VY | CHIP-8 | Set VX equal to VY bitshifted left 1. VF is set to the most significant bit of VX prior to the shift.
        private void ShlRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            WriteRegister(index: 15, (vX >> 7) & 1);

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                vX = vY << 1;
            }
            else /* if (CompatibilityMode == CompatibilityMode.SChip) */
            {
                vX = vX << 1;
            }

            WriteRegister(xIdx, vX & 255);
        }

        // 9XY0 | SNE VX, VY | CHIP-8 | Skip the next instruction if VX does not equal VY.
        private void SkipIfNotEqualRegReg(int xIdx, int yIdx)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            if (vX != vY)
            {
                IncPc();
            }
        }

        // ANNN | LD I, NNN | CHIP-8 | Set I equal to NNN.
        private void SetI(int nnn)
        {
            WriteI(nnn);
        }

        // BNNN | JMP V0, NNN | CHIP-8 | Set the PC to NNN plus the value in V0.
        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_jump0.md
        private void JumpPlusU(int nnn)
        {
            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                int v0 = ReadRegister(index: 0);

                WritePc(nnn + v0 - 2);
            }
            else /* if (CompatibilityMode == CompatibilityMode.SChip) */
            {
                int nIdx = (nnn >> 12) & 0b1111;

                int vX = ReadRegister(nIdx);

                WritePc(nnn + vX - 2);
            }
        }

        // CXNN | RND VX, NN | CHIP-8 | Set VX equal to a random number ranging from 0 to 255 which is logically anded with NN.
        private void RandRegImm(int xIdx, int nn)
        {
            Trace.Assert(nn >= 0 && nn <= 255);

            int value = _rnd.Next(0, 255 + 1);

            WriteRegister(xIdx, value & nn);
        }

        // DXYN | DRW VX, VY, N | CHIP-8 | Display N-byte sprite starting at memory location I at (VX, VY). Each set bit of xored with what's already drawn. VF is set to 1 if a collision occurs. 0 otherwise.
        // DXY0 | DRW VX, VX, 0 | SCHIP-8 | When in high res mode show a 16x16 sprite at (VX, VY).
        private void DrawSprite(int xIdx, int yIdx, int n, out bool needsSync)
        {
            int vX = ReadRegister(xIdx);
            int vY = ReadRegister(yIdx);

            Trace.Assert(n >= 0 && n <= 15);

            needsSync = false;

            Trace.Assert(n != 0 || CompatibilityMode == CompatibilityMode.SChip);

            if (n != 0)
            {
                int[] spriteData = new int[n];

                for (int i = 0; i < n; i++)
                {
                    spriteData[i] = ReadMemory(_i + i);
                }

                _gpu.DrawSprite(vX, vY, spriteData, bitsPerRow: 8, out bool isCollision);

                WriteRegister(index: 15, isCollision ? 1 : 0);

                if (CompatibilityMode == CompatibilityMode.Vip)
                {
                    needsSync = true;
                }
            }
            else if (CompatibilityMode == CompatibilityMode.SChip)
            {
                n = 16;

                if (_gpu.SChipMode == SChipMode.LowRes)
                {
                    int[] spriteData = new int[n];

                    for (int i = 0; i < n; i++)
                    {
                        spriteData[i] = ReadMemory(_i + i);
                    }

                    _gpu.DrawSprite(vX, vY, spriteData, bitsPerRow: 8, out bool isCollision);

                    WriteRegister(index: 15, isCollision ? 1 : 0);
                }
                else /* if (_gpu.SChipMode == SChipMode.HighRes) */
                {
                    int[] spriteData = new int[n];

                    for (int i = 0, j = 0; i < n; i++, j += 2)
                    {
                        spriteData[i] = ReadMemory(_i + j) << 8 | ReadMemory(_i + j + 1);
                    }

                    _gpu.DrawSprite(vX, vY, spriteData, bitsPerRow: 16, out bool isCollision);

                    WriteRegister(index: 15, isCollision ? 1 : 0);
                }
            }
        }

        // EX9E | SKP VX | CHIP-8 | Skip the following instruction if the key represented by the value in VX is pressed.
        private void SkipIfKey(int xIdx)
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(_keypad.IsKeyValid(vX));

            if (_keypad.IsKeyPressed(vX))
            {
                IncPc();
            }

            _keypad.AddKeyHint(vX);
        }

        // EXA1 | SKNP VX | CHIP-8 | Skip the following instruction if the key represented by the value in VX is not pressed.
        private void SkipIfNotKey(int xIdx)
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(_keypad.IsKeyValid(vX));

            if (!_keypad.IsKeyPressed(vX))
            {
                IncPc();
            }

            _keypad.AddKeyHint(vX);
        }

        // FX07 | LD VX, DT | CHIP-8 | Set VX equal to the delay timer.
        private void GetDelayTimer(int xIdx)
        {
            WriteRegister(xIdx, ReadTimer(TimerType.Delay));
        }

        // FX0A | LD VX, KEY | CHIP-8 | Wait for a key press and store the value of the key into VX.
        private void WaitAnyKey(int xIdx, out bool needsSync)
        {
            if (_keypad.IsOneKeyJustPressed(out int key))
            {
                WriteRegister(xIdx, key);

                _keypad.Waiting = false;

                needsSync = false;
            }
            else
            {
                WritePc(_pc - 2);

                _keypad.Waiting = true;

                needsSync = true;
            }
        }

        // FX15 | LD DT, VX | CHIP-8 | Set the delay timer DT to VX.
        private void SetDelayTimer(int xIdx)
        {
            WriteTimer(TimerType.Delay, ReadRegister(xIdx));
        }

        // FX18 | LD ST, VX | CHIP-8 | Set the sound timer ST to VX.
        private void SetSoundTimer(int xIdx)
        {
            WriteTimer(TimerType.Sound, ReadRegister(xIdx));
        }

        // FX1E | ADD I, VX | CHIP-8 | Add VX to I.
        // (VF is set to 1 if I > 0x0FFF. Otherwise set to 0).
        private void AddI(int xIdx)
        {
            int vX = ReadRegister(xIdx);

            WriteI(_i + vX);
        }

        // FX29 | LD I, FONT(VX) | CHIP-8 | Set I to the address of the CHIP-8 8x5 font sprite representing the value in VX.
        private void LoadFont(int xIdx) // Not for use with DRW Vx, Vy, 0 (Dxy0).
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(vX <= 0xF);

            int fontAddress = FontDataStart + vX * 5;

            WriteI(fontAddress);
        }

        // FX33 | BCD VX | CHIP-8 | Convert that word to BCD and store the 3 digits at memory location I through I+2. I does not change.
        private void Bcd(int xIdx)
        {
            int vX = ReadRegister(xIdx);

            int hundreds = vX / 100;
            int tens = (vX % 100) / 10;
            int units = vX % 10;

            WriteMemory(_i, hundreds);
            WriteMemory(_i + 1, tens);
            WriteMemory(_i + 2, units);
        }

        // FX55 | LD [I], VX | CHIP-8 | Store registers V0 through VX in memory starting at location I.
        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_i.md
        private void Store(int xIdx)
        {
            for (int j = 0; j <= xIdx; j++)
            {
                int vJ = ReadRegister(j);

                WriteMemory(_i + j, vJ);
            }

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                WriteI(_i + xIdx + 1);
            }
        }

        // FX65 | LD VX, [I] | CHIP-8 | Copy values from memory location I through I + X into registers V0 through VX.
        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_i.md
        private void Load(int xIdx)
        {
            for (int j = 0; j <= xIdx; j++)
            {
                int mJ = ReadMemory(_i + j);

                WriteRegister(j, mJ);
            }

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                WriteI(_i + xIdx + 1);
            }
        }

        /* Super Chip-48 Instructions. */

        // 00CN | SCD N | SCHIP-8 | Scroll display N lines down.
        private void ScrollDown(int n, out bool needsSync)
        {
            needsSync = false;

            if (n != 0)
            {
                _gpu.Scroll(ScrollDirection.Down, amount: n);

                if (CompatibilityMode == CompatibilityMode.Vip)
                {
                    needsSync = true;
                }
            }
        }

        // 00FB | SCR | SCHIP-8 | Scroll display 4 pixels to the right.
        private void ScrollRight(out bool needsSync)
        {
            needsSync = false;

            _gpu.Scroll(ScrollDirection.Right, amount: 4);

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                needsSync = true;
            }
        }

        // 00FC | SCL | SCHIP-8 | Scroll display 4 pixels to the left.
        private void ScrollLeft(out bool needsSync)
        {
            needsSync = false;

            _gpu.Scroll(ScrollDirection.Left, amount: 4);

            if (CompatibilityMode == CompatibilityMode.Vip)
            {
                needsSync = true;
            }
        }

        // 00FD | EXIT | SCHIP-8 | Exit the interpreter.
        private void Exit(out bool exitGuest)
        {
            exitGuest = true;
        }

        // 00FE | LOW | SCHIP-8 | Enable low res (64x32) mode.
        private void LowRes(out bool needsSync)
        {
            needsSync = true;

            Trace.Assert(CompatibilityMode == CompatibilityMode.SChip);

            _gpu.SetSChipMode(SChipMode.LowRes);
        }

        // 00FF | HIGH | SCHIP-8 | Enable high res (128x64) mode.
        private void HighRes(out bool needsSync)
        {
            needsSync = true;

            Trace.Assert(CompatibilityMode == CompatibilityMode.SChip);

            _gpu.SetSChipMode(SChipMode.HighRes);
        }

        // FX30 | LD I, FONT(VX) | SCHIP-8 | Set I to the address of the SCHIP-8 16x10 font sprite representing the value in VX.
        // https://github.com/Chromatophore/HP48-Superchip/blob/master/investigations/quirk_font.md
        private void LoadHighFont(int xIdx) // Not for use with DRW Vx, Vy, 0 (Dxy0).
        {
            int vX = ReadRegister(xIdx);

            Trace.Assert(vX <= 0xF); // vX <= 9.

            int fontAddress = HighFontDataStart + vX * 10;

            WriteI(fontAddress);
        }

        // FX75 | LD R, VX | SCHIP-8 | Store V0 through VX to HP-48 RPL user flags (X <= 7).
        private void StoreRpl(int xIdx)
        {
            for (int j = 0; j <= xIdx; j++)
            {
                int vJ = ReadRegister(j);

                WriteRpl(j, vJ);
            }
        }

        // FX85 | LD VX, R | SCHIP-8 | Read V0 through VX to HP-48 RPL user flags (X <= 7).
        private void LoadRpl(int xIdx)
        {
            for (int j = 0; j <= xIdx; j++)
            {
                int rJ = ReadRpl(j);

                WriteRegister(j, rJ);
            }
        }

        /**/

        public int ReadMemory(int address)
        {
            Trace.Assert(address >= DataStart && address <= CodeDataEndMax);

            int value = _mem[address];

            Trace.Assert(value >= 0 && value <= 255);

            return value;
        }

        private void WriteMemory(int address, int value)
        {
            Trace.Assert(address >= CodeDataStart && address <= CodeDataEndMax);
            Trace.Assert(value >= 0 && value <= 255);

            _mem[address] = value;
        }

        public int ReadPc()
        {
            return _pc;
        }

        private void WritePc(int address)
        {
            Trace.Assert(address >= CodeDataStart - 2 && address <= CodeDataEnd);

            //Trace.Assert((address & 1) == 0);

            _pc = address;
        }

        private void IncPc()
        {
            int address = _pc + 2;

            Trace.Assert(address >= CodeDataStart && address <= CodeDataEnd);

            //Trace.Assert((address & 1) == 0);

            _pc = address;
        }

        public int ReadI()
        {
            return _i;
        }

        private void WriteI(int address)
        {
            Trace.Assert(address >= DataStart && address <= CodeDataEndMax);

            _i = address;
        }

        public int ReadRegister(int index)
        {
            Trace.Assert(index >= 0 && index <= 15);

            int value = _v[index];

            Trace.Assert(value >= 0 && value <= 255);

            return value;
        }

        private void WriteRegister(int index, int value)
        {
            Trace.Assert(index >= 0 && index <= 15);
            Trace.Assert(value >= 0 && value <= 255);

            _v[index] = value;
        }

        public int ReadRpl(int index)
        {
            Trace.Assert(index >= 0 && index <= 7);

            int value = _rpl[index];

            Trace.Assert(value >= 0 && value <= 255);

            return value;
        }

        public void WriteRpl(int index, int value)
        {
            Trace.Assert(index >= 0 && index <= 7);
            Trace.Assert(value >= 0 && value <= 255);

            _rpl[index] = value;
        }

        public int ReadTimer(TimerType timerType)
        {
            if (timerType == TimerType.Delay)
            {
                int value = _dt.GetValue();

                Trace.Assert(value >= 0 && value <= 255);

                return value;
            }
            else /* if (timerType == TimerType.Sound) */
            {
                int value = _st.GetValue();

                Trace.Assert(value >= 0 && value <= 255);

                return value;
            }
        }

        private void WriteTimer(TimerType timerType, int value)
        {
            if (timerType == TimerType.Delay)
            {
                Trace.Assert(value >= 0 && value <= 255);

                _dt.SetValue(value);
            }
            else /* if (timerType == TimerType.Sound) */
            {
                Trace.Assert(value >= 0 && value <= 255);

                _st.SetValue(value);
            }
        }
    }
}