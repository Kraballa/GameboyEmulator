using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class Registers
    {
        private ushort[] regs = new ushort[6];

        public Flags Flags
        {
            get => (Flags)GetLow(0);
            set => SetLow(0, (byte)value);
        }

        public ushort AF
        {
            get => regs[0];
            set => regs[0] = value;
        }

        public byte A
        {
            get => GetHigh(0);
            set => SetHigh(0, value);
        }

        public ushort BC
        {
            get => regs[1];
            set => regs[1] = value;
        }

        public byte B
        {
            get => GetHigh(1);
            set => SetHigh(1, value);
        }

        public byte C
        {
            get => (byte)regs[1];
            set => SetLow(1, value);
        }

        public ushort DE
        {
            get => regs[2];
            set => regs[2] = value;
        }

        public byte D
        {
            get => GetHigh(2);
            set => SetHigh(2, value);
        }

        public byte E
        {
            get => (byte)regs[2];
            set => SetLow(2, value);
        }

        public ushort HL
        {
            get => regs[3];
            set => regs[3] = value;
        }

        public byte H
        {
            get => GetHigh(3);
            set => SetHigh(3, value);
        }

        public byte L
        {
            get => (byte)regs[3];
            set => SetLow(3, value);
        }

        public ushort SP
        {
            get => regs[4];
            set => regs[4] = value;
        }

        public ushort PC
        {
            get => regs[5];
            set => regs[5] = value;
        }

        public byte GetHigh(uint index)
        {
            return (byte)(regs[index] >> 8);
        }

        public byte GetLow(uint index)
        {
            return (byte)regs[index];
        }

        public void SetHigh(uint index, int value)
        {
            value &= 0xFF;
            regs[index] = (ushort)((value << 8) | (regs[index] & 0xFF));
        }

        public void SetLow(uint index, byte value)
        {
            regs[index] = (ushort)(value | (regs[index] & 0xFF00));
        }

        public int GetByte(uint index)
        {
            switch (index)
            {
                default:
                case 0:
                    return A;
                case 1:
                    return B;
                case 2:
                    return C;
                case 3:
                    return D;
                case 4:
                    return E;
                case 5:
                    return H;
                case 6:
                    return L;
                case 8:
                    return A;
            }
        }

        public void SetByte(uint index, int value)
        {
            switch (index)
            {
                default:
                case 8:
                case 0:
                    A = (byte)(value & 0xFF);
                    break;
                case 1:
                    B = (byte)(value & 0xFF);
                    break;
                case 2:
                    C = (byte)(value & 0xFF);
                    break;
                case 3:
                    D = (byte)(value & 0xFF);
                    break;
                case 4:
                    E = (byte)(value & 0xFF);
                    break;
                case 5:
                    H = (byte)(value & 0xFF);
                    break;
                case 6:
                    L = (byte)(value & 0xFF);
                    break;
            }
        }

        public ushort this[uint index]
        {
            get => regs[index];
            set => regs[index] = value;
        }

        public void FlushFlags(Flags flags = 0)
        {
            Flags = flags;
        }

        public bool Place(bool value, Flags flag)
        {
            if (value)
            {
                Set(flag);
                return true;
            }
            else
            {
                Unset(flag);
                return false;
            }
        }

        public void Set(Flags flags)
        {
            Flags |= flags;
        }

        public void Unset(Flags flags)
        {
            Flags = Flags & ~flags;
        }

        public bool IsSet(Flags flag)
        {
            return (Flags & flag) == flag;
        }

        public void Flip(Flags flags)
        {
            Flags = (Flags & ~flags) | (~Flags & flags);
        }

        public bool CheckHCarry(byte a, byte b)
        {
            byte result = (byte)(a + b);
            return Place(((a ^ b ^ result) & 0x10) > 0, Flags.HCARRY);
        }

        public bool CheckHCarryDec(byte a, byte b)
        {
            byte result = (byte)(a - b);
            return Place(((a ^ (-b) ^ result) & 0x10) > 0, Flags.HCARRY);
        }

        public bool CheckHCarry(ushort a, ushort b)
        {
            ushort result = (ushort)(a + b);
            return Place(((a ^ b ^ result) & 0x1000) > 0, Flags.HCARRY);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("A: 0x{0:X}", A));
            sb.AppendLine(string.Format("BC: 0x{0:X}", BC));
            sb.AppendLine(string.Format("DE: 0x{0:X}", DE));
            sb.AppendLine(string.Format("HL: 0x{0:X}", HL));
            sb.AppendLine(string.Format("SP: 0x{0:X}", SP));
            sb.Append(string.Format("PC: 0x{0:X}", PC));
            return sb.ToString();
        }
    }
}
