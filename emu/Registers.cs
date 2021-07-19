using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class Registers
    {
        private ushort[] regs = new ushort[6];

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

        public void SetHigh(uint index, byte value)
        {
            regs[index] = (ushort)((value << 8) | (regs[index] & 0x0f));
        }

        public void SetLow(uint index, byte value)
        {
            regs[index] = (ushort)(value | (regs[index] & 0xf0));
        }

        public ushort this[uint index]
        {
            get => regs[index];
            set => regs[index] = value;
        }
    }
}
