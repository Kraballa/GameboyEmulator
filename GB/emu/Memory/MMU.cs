﻿using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu.Memory
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

    public enum SerialControl
    {
        TRANSFER_START = 0b10000000,
        SHIFT_CLOCK = 0b00000001
    }

    public class MMU
    {
        public const ushort BANK0 = 0x0000;
        public const ushort BANK1 = 0x4000;
        public const ushort VRAM = 0x8000;
        public const ushort ExRAM = 0xA000;
        public const ushort WRAM0 = 0xC000;
        public const ushort WRAM1 = 0xD000;
        public const ushort ERAM = 0xE000; //echo ram, mirror of both WRAMS
        public const ushort OAM = 0xFE00; //sprite attribute table, called OAM (object attribute memory)
        public const ushort OAMEnd = 0xFE9F;
        public const ushort IO = 0xFF00;
        public const ushort SERIALDATA = 0xFF01; //8bit data that is being transferred
        public const ushort SERIALTC = 0xFF02; //transfer control register. writing bit 7 = 1 initiates serial transfer
        public const ushort DMA = 0xFF46;
        public const ushort HiRAM = 0xFF80;
        public const ushort IFREG = 0xFF0F; //Interrupt Request Register
        public const ushort IEREG = 0xFFFF; //Interrupt Enable Register

        public Rom Rom;
        public bool IMEF { get; set; } //Interrupt Master Enable Flag. TODO: figure out where exactly this is located

        public byte[] Mem { get => mem; } //only use for reading VRAM, OAM or Debugging

        public Action<byte> ReceiveSerialByte;

        private byte[] mem = new byte[0x10000];

        public byte this[ushort index]
        {
            get
            {
                if (index < VRAM)
                    return Rom[index];
                return mem[index];
            }
            set
            {
                switch (index)
                {
                    default:
                        mem[index] = value;
                        break;
                    case Timer.DIV:
                        mem[index] = 0; return;
                    case IO:
                        mem[index] = value;
                        CPU.Instance.Input.GetInputCallback(value);
                        break;
                    case DMA:
                        mem[index] = value;
                        OamDmaTransfer(value);
                        break;
                    case SERIALTC:
                        if ((this[SERIALTC] & (byte)SerialControl.TRANSFER_START) > 0)
                        {
                            ReceiveSerialByte?.Invoke(this[SERIALDATA]);
                            this[SERIALTC] &= 0b01111111;
                            CPU.Instance.RequestInterrupt(InterruptType.SERIAL);
                        }
                        break;
                }
            }
        }

        public MMU()
        {
            Rom = CPU.Instance.Rom;

            for (int i = 0; i < 48; i++)
            {
                this[(ushort)(VRAM + i)] = this[(ushort)(0x104 + i)];
            }
        }

        public void Push(ushort data)
        {
            this[CPU.Instance.Regs.SP] = (byte)((data & 0xFF00) >> 8);
            CPU.Instance.Regs.SP--;
            this[CPU.Instance.Regs.SP] = (byte)(data & 0xFF);
            CPU.Instance.Regs.SP--;
        }

        public ushort Pop()
        {
            CPU.Instance.Regs.SP++;
            ushort data = this[CPU.Instance.Regs.SP];
            CPU.Instance.Regs.SP++;
            data |= (ushort)(this[CPU.Instance.Regs.SP] << 8);
            return data;
        }

        /// <summary>
        /// Load sprite data from ROM or RAM to OAM (sprite attribute table)
        /// </summary>
        private void OamDmaTransfer(byte written)
        {
            ushort start = (ushort)(written << 8);
            for (ushort offset = 0x00; offset <= 0x9F; offset++)
            {
                this[(ushort)(OAM | offset)] = this[(ushort)(start | offset)];
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
        /// Get the memory area that is being accessed. SLOW
        /// TODO: replace with Range Tree or similar efficient data structure
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
