using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class Memory
    {
        //BANK0 starts at 0x00 so it's left out here
        public const ushort BANK1 = 0x4000;
        public const ushort VRAM = 0x8000;
        public const ushort ExRAM = 0xA000;
        public const ushort WRAM0 = 0xC000;
        public const ushort WRAM1 = 0xD000;
        public const ushort ERAM = 0xE000; //echo ram, mirror of both WRAMS
        public const ushort OAM = 0xFE00; //sprite attribute table, called OAM (object attribute memory)
        public const ushort HiRAM = 0xFF80;
        public const ushort IEREG = 0xFFFF;

        public byte IE
        {
            get => mem[0xFFFF];
            set => mem[0xFFFF] = value;
        }

        private byte[] mem = new byte[0xFFFF];

        public byte this[uint index]
        {
            get => mem[index];
            set => mem[index] = value;
        }
    }
}
