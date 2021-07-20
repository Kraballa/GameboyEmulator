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
        public const ushort IFREG = 0xFF0F; //Interrupt Request Register
        public const ushort IEREG = 0xFFFF; //Interrupt Enable Register

        public Dictionary<ushort, Action> MemoryAccessCallback = new Dictionary<ushort, Action>();

        public byte IE
        {
            get => mem[0xFFFF];
            set => mem[0xFFFF] = value;
        }

        public bool IMEF { get; set; } //Interrupt Master Enable Flag. TODO: figure out where exactly this is located

        private byte[] mem = new byte[0xFFFF];

        public byte this[ushort index]
        {
            get
            {
                if (!IsAccessible(index))
                    return 0xFF;
                return mem[index];
            }
            set
            {
                if (!IsAccessible(index))
                    return;
                mem[index] = value;
                if (MemoryAccessCallback.ContainsKey(index))
                    MemoryAccessCallback[index].Invoke();
            }
        }

        public bool IsAccessible(ushort address)
        {
            byte vramMode = (byte)(this[Display.LCDS] & (byte)LCDSReg.Mode);
            //VRAM is only accessible in modes 0-2
            if (vramMode < 3 && address >= VRAM && address < ExRAM)
                return false;

            //OAM is only accessible in modes 0-1
            if ((vramMode & 0b10) == 0 && address >= OAM && address < HiRAM)
                return false;

            return true;
        }
    }
}
