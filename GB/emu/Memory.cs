using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    /// <summary>
    /// Used both for enabling/disabling Interrupts as well as Requests in IEREG and IFREG
    /// </summary>
    public enum Interrupt
    {
        VBLANK = 0b1,
        LCDSTAT = 0b10,
        TIMER = 0b100,
        SERIAL = 0b1000,
        JOYPAD = 0b10000
    }

    public enum MemoryArea
    {
        Any,
        BANK0,
        BANK1,
        VRAM,
        ExRAM,
        WRAM0,
        WRAM1,
        ERAM,
        OAM,
        IO,
        HiRAM,
        IEREG,
        Unusable,
    }

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
        public const ushort OAMEnd = 0xFE9F;
        public const ushort IO = 0xFF00;
        public const ushort HiRAM = 0xFF80;
        public const ushort IFREG = 0xFF0F; //Interrupt Request Register
        public const ushort IEREG = 0xFFFF; //Interrupt Enable Register

        public Dictionary<ushort, Action<byte>> MemoryAccessCallback = new Dictionary<ushort, Action<byte>>();

        public byte IE
        {
            get => mem[0xFFFF];
            set => mem[0xFFFF] = value;
        }

        public bool IMEF { get; set; } //Interrupt Master Enable Flag. TODO: figure out where exactly this is located

        //only use for reading VRAM, OAM or Debugging
        public byte[] Mem { get => mem; }

        private byte[] mem = new byte[0x10000];

        /// <summary>
        /// Easy access to memory read and write
        /// </summary>
        public byte this[ushort index]
        {
            get
            {
                return mem[index];
            }
            set
            {
                mem[index] = value;
                if (MemoryAccessCallback.ContainsKey(index))
                    MemoryAccessCallback[index].Invoke(value);
            }
        }

        /// <summary>
        /// Check whether the given memory area is accessible.
        /// </summary>
        /// <returns>any if it's accesssible, the designated memory area if not</returns>
        public MemoryArea IsAccessible(ushort address)
        {
            byte vramMode = (byte)(mem[Display.LCDS] & (byte)LCDSReg.Mode);
            //VRAM is only accessible in modes 0-2
            if (vramMode < 3 && GetMemoryArea(address) == MemoryArea.VRAM)
                return MemoryArea.VRAM;

            //OAM is only accessible in modes 0-1
            if ((vramMode & 0b10) == 0 && GetMemoryArea(address) == MemoryArea.OAM)
                return MemoryArea.OAM;

            return MemoryArea.Any;
        }

        /// <summary>
        /// Get the memory area that is being accessed.
        /// </summary>
        public MemoryArea GetMemoryArea(ushort index)
        {
            if (index >= IEREG)
                return MemoryArea.IEREG;
            else if (index >= HiRAM)
                return MemoryArea.HiRAM;
            else if (index >= IO)
                return MemoryArea.IO;
            else if (index >= 0xFEA0)
                return MemoryArea.Unusable;
            else if (index >= OAM)
                return MemoryArea.OAM;
            else if (index >= ERAM)
                return MemoryArea.ERAM;
            else if (index >= WRAM1)
                return MemoryArea.WRAM1;
            else if (index >= WRAM0)
                return MemoryArea.WRAM0;
            else if (index >= ExRAM)
                return MemoryArea.ExRAM;
            else if (index >= VRAM)
                return MemoryArea.VRAM;
            else if (index >= BANK1)
                return MemoryArea.BANK1;
            else
                return MemoryArea.BANK0;
        }
    }
}
