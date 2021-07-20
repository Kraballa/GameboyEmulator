using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum Palette
    {
        White = 0b00000011,
        LightGray = 0b00001100,
        DarkGray = 0b00110000,
        Black = 0b11000000
    }

    public class LCD
    {
        public const ushort SCY = 0xFF42; //bg map scroll y
        public const ushort SCX = 0xFF43; //bg map scroll x
        public const ushort LY = 0xFF44; // [0;153], vertical line to which data is transferred.
        public const ushort LYC = 0xFF45;
        public const ushort WY = 0xFF4A; // window y position
        public const ushort WX = 0xFF4B; // window x position - 7
        public const ushort DMA = 0xFF46; // DMA transfer start address

        //need access to memory to write to and read from
        private Memory Memory;

        public LCD(Memory memory)
        {
            Memory = memory;

            HookToMemory();
        }

        /// <summary>
        /// Load sprite data from ROM or RAM to OAM (sprite attribute table)
        /// </summary>
        public void OamDmaTransfer()
        {
            ushort start = (ushort)(Memory[DMA] << 8);
            for (ushort offset = 0x00; offset < 0x9F; offset++)
            {
                Memory[(ushort)(Memory.OAM | offset)] = Memory[(ushort)(start | offset)];
            }
        }

        private void HookToMemory()
        {
            //when this location is written to, transfer sprite data from ROM or RAM to OAM
            Memory.MemoryAccessCallback.Add(DMA, OamDmaTransfer);
        }
    }
}
