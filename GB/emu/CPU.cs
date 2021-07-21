﻿using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum OCErrorMode
    {
        ERROR,
        PRINT,
        NOTHING
    }

    public enum CPUMode
    {
        NORMAL,
        HALT,
        STOP
    }

    public class CPU
    {
        public OCErrorMode OCErrorMode = OCErrorMode.ERROR;

        public Flags Flags = 0;
        public Registers Regs;
        public Memory Memory;
        public Rom Rom;
        public Display LCD;
        public Input Input;

        protected CPUMode CPUMode = CPUMode.NORMAL;
        protected int Clock = 0;


        public CPU(Rom rom)
        {
            Rom = rom;
            Regs = new Registers();
            Memory = new Memory(rom);
            LCD = new Display(Memory);
            Input = new Input(Memory);

            //skip starting sequence and jump straight to cartridge start
            Regs.PC = 0x100;
        }

        /// <summary>
        /// one frame, as in the period between 2 VBlanks
        /// 
        /// Occurs every 70224 clocks, VBlank last 4560.
        /// </summary>
        public virtual void Step()
        {
            while (Clock < 70224 && CPUMode == CPUMode.NORMAL)
            {
                //not accurate
                Clock += Execute(Fetch()) * 4;
            }
            Clock -= 70224;
        }

        protected virtual byte Fetch()
        {
            return Memory[Regs.PC++];
        }

        protected virtual int Execute(byte opcode)
        {
            uint HighBit = (uint)(opcode >> 4);
            byte Data;
            int Cycles = 0;

            switch (opcode)
            {
                #region Special Operations
                case 0x00: //NOP
                    Cycles = 1;
                    break;
                case 0x10: //STOP
                    CPUMode = CPUMode.STOP;
                    Fetch();
                    Cycles = 1;
                    break;
                case 0x76: //HALT
                    CPUMode = CPUMode.HALT;
                    Cycles = 1;
                    break;
                case 0xF3: //Disable Interrupts via Interrupt Master Enable Flag
                    Memory.IMEF = false;
                    Cycles = 1;
                    break;
                case 0xFB: //Enable Interrupts via Interrupt Master Enable Flag
                    Memory.IMEF = true;
                    Cycles = 1;
                    break;
                case 0xD9: //RETI
                    Memory.IMEF = true;
                    //TODO: same code as RET
                    Cycles = 4;
                    break;
                case 0xCB:
                    Cycles = Execute16Bit(Fetch());
                    break;
                #endregion

                #region JR NX, s8
                case 0x20:
                    Cycles = 2;
                    if (!IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)(Regs.PC - 1 + Fetch());
                        Cycles++;
                    }
                    break;
                case 0x30:
                    Cycles = 2;
                    if (!IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)(Regs.PC - 1 + Fetch());
                        Cycles++;
                    }
                    break;

                #endregion

                #region LD XY, d16
                case 0x01:
                case 0x11:
                case 0x21:
                case 0x31:
                    Regs[HighBit + 1] = (ushort)(Fetch() | Fetch() << 8);
                    Cycles = 3;
                    break;
                #endregion

                #region LD (XY), A
                case 0x02:
                case 0x12:
                    Memory[Regs[HighBit + 1]] = Regs.A;
                    Cycles = 2;
                    break;
                case 0x22:
                    Memory[Regs.HL++] = Regs.A;
                    Cycles = 2;
                    break;
                case 0x32:
                    Memory[Regs.HL--] = Regs.A;
                    Cycles = 2;
                    break;
                #endregion

                #region INC XY
                case 0x03:
                case 0x13:
                case 0x23:
                case 0x33:
                    Regs[HighBit + 1]++;
                    Cycles = 2;
                    break;
                #endregion

                #region INC X
                case 0x04:
                case 0x14:
                case 0x24:
                    Regs.SetHigh(HighBit + 1, (byte)(Regs.GetHigh(HighBit + 1) + 1)); //++
                    Unset(Flags.SUB);
                    if (Regs.GetHigh(HighBit + 1) == 0)
                        Set(Flags.ZERO | Flags.HCARRY);
                    Cycles = 1;
                    break;
                case 0x34:
                    Memory[Regs.HL]++;
                    Unset(Flags.SUB);
                    if (Regs.B == 0)
                        Set(Flags.ZERO | Flags.HCARRY);
                    Cycles = 3;
                    break;
                #endregion

                #region DEC X
                case 0x05:
                case 0x15:
                case 0x25:
                    Place(Regs.GetHigh(HighBit + 1) == 0, Flags.HCARRY);
                    Regs.SetHigh(HighBit + 1, (byte)(Regs.GetHigh(HighBit + 1) - 1)); //--
                    Set(Flags.SUB);
                    Place(Regs.GetHigh(HighBit + 1) == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x35:
                    Place(Memory[Regs.HL] == 0, Flags.HCARRY);
                    Memory[Regs.HL]--;
                    Set(Flags.SUB);
                    Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 3;
                    break;
                #endregion

                #region LD X, d8
                case 0x06:
                case 0x16:
                case 0x26:
                    Regs.SetHigh(HighBit + 1, Fetch());
                    Cycles = 2;
                    break;
                case 0x36:
                    Memory[Regs.HL] = Fetch();
                    Cycles = 3;
                    break;
                #endregion

                #region RL(C)A
                case 0x07:
                    Data = (byte)(Regs.A >> 7);
                    Regs.A <<= 1;
                    Regs.A |= Data;
                    FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A7 to CY
                    Cycles = 1;
                    break;
                case 0x17:
                    Data = (byte)(Regs.A >> 7);
                    Regs.A <<= 1;
                    Regs.A |= (byte)(IsSet(Flags.CARRY) ? 1 : 0); // write CY to A0
                    FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A7 to CY
                    Cycles = 1;
                    break;
                #endregion

                case 0x27: //DAA. see https://forums.nesdev.com/viewtopic.php?t=15944
                    if (!IsSet(Flags.SUB))
                    {
                        if (IsSet(Flags.CARRY) || Regs.A > 0x99)
                        {
                            Regs.A += 0x60;
                            Set(Flags.CARRY);
                        }
                        if (IsSet(Flags.HCARRY) || (Regs.A & 0x0F) > 0x09)
                        {
                            Regs.A += 0x6;
                        }
                    }
                    else
                    {
                        if (IsSet(Flags.CARRY))
                            Regs.A -= 0x60;
                        if (IsSet(Flags.HCARRY))
                            Regs.A -= 0x6;
                    }
                    Cycles = 1;
                    Place(Regs.A == 0, Flags.ZERO);
                    Unset(Flags.HCARRY);
                    break;

                case 0x37:
                    FlushFlags(Flags.CARRY | (Flags & Flags.ZERO)); //corresponds to -001 i.e. ignore zero, set Carry, rest 0
                    Cycles = 1;
                    break;

                case 0x08:
                    ushort address = (ushort)((Fetch() << 8) | Fetch());
                    Memory[address] = Regs.GetLow(4);
                    Memory[address] = Regs.GetHigh(4);
                    Cycles = 5;
                    break;

                #region JR [0|Z|C], s8
                case 0x18:
                    Regs.PC = (ushort)(Regs.PC - 1 + Fetch());
                    Cycles = 3;
                    break;
                case 0x28:
                    Cycles = 2;
                    if (IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)(Regs.PC - 1 + Fetch());
                        Cycles++;
                    }
                    break;
                case 0x38:
                    Cycles = 2;
                    if (IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)(Regs.PC - 1 + Fetch());
                        Cycles++;
                    }
                    break;
                #endregion

                #region ADD HL, XY
                case 0x09:
                case 0x19:
                case 0x29:
                case 0x39:
                    Place(Regs.HL + Regs[HighBit + 1] > ushort.MaxValue, Flags.CARRY | Flags.HCARRY);
                    Regs.HL += Regs[HighBit + 1];
                    Unset(Flags.SUB);
                    Cycles = 2;
                    break;
                #endregion

                #region LD A, (XY)
                case 0x0A:
                case 0x1A:
                    Regs.A = Memory[Regs[HighBit + 1]];
                    Cycles = 2;
                    break;
                case 0x2A:
                    Regs.A = Memory[Regs.HL++];
                    Cycles = 2;
                    break;
                case 0x3A:
                    Regs.A = Memory[Regs.HL--];
                    Cycles = 2;
                    break;
                #endregion

                #region DEC XY
                case 0x0B:
                case 0x1B:
                case 0x2B:
                case 0x3B:
                    Regs[HighBit + 1]--;
                    Cycles = 2;
                    break;
                #endregion

                #region INC Y
                case 0x0C:
                case 0x1C:
                case 0x2C:
                    Regs.SetLow(HighBit + 1, (byte)(Regs.GetLow(HighBit + 1) + 1)); //++
                    Unset(Flags.SUB);
                    Place(Regs.GetLow(HighBit + 1) == 0, Flags.ZERO | Flags.HCARRY);
                    Cycles = 1;
                    break;
                case 0x3C:
                    Regs.A++;
                    Unset(Flags.SUB);
                    Place(Regs.A == 0, Flags.ZERO | Flags.HCARRY);
                    Cycles = 1;
                    break;
                #endregion

                #region DEC Y
                case 0x0D:
                case 0x1D:
                case 0x2D:
                    Place(Regs.GetLow(HighBit + 1) == 0, Flags.HCARRY);
                    Regs.SetLow(HighBit + 1, (byte)(Regs.GetLow(HighBit + 1) - 1)); //--
                    Set(Flags.SUB);
                    Place(Regs.GetLow(HighBit + 1) == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x3D:
                    Place(Regs.A == 0, Flags.HCARRY);
                    Regs.A--;
                    Set(Flags.SUB);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                #endregion

                #region LD Y, d8
                case 0x0E:
                case 0x1E:
                case 0x2E:
                    Regs.SetLow(HighBit + 1, Fetch());
                    Cycles = 2;
                    break;
                case 0x3E:
                    Regs.A = Fetch();
                    Cycles = 2;
                    break;
                #endregion

                #region RR(C)A
                case 0x0F:
                    Data = (byte)(Regs.A & 0x1);
                    Regs.A >>= 1;
                    Regs.A |= (byte)(Data << 7);
                    FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A0 to CY
                    Cycles = 1;
                    break;
                case 0x1F:
                    Data = (byte)(Regs.A & 0x1);
                    Regs.A >>= 1;
                    FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A0 to CY
                    Cycles = 1;
                    break;
                #endregion

                case 0x2F:
                    Regs.A ^= 0b11111111; //flip all bits
                    Set(Flags.SUB | Flags.HCARRY);
                    Cycles = 1;
                    break;
                case 0x3F:
                    Unset(Flags.SUB | Flags.HCARRY);
                    Flags ^= Flags.CARRY;
                    break;

                default: //unknown opcode
                    HandleUnknownOpcode(opcode);
                    break;
            }
            return Cycles;
        }

        protected virtual int Execute16Bit(byte opcode) //always prefixed with 'CB'
        {
            int Cycles = 0;
            switch (opcode)
            {
                default: //unknown opcode
                    HandleUnknownOpcode(opcode);
                    break;
            }
            return Cycles;
        }

        private void FlushFlags(Flags flags = 0)
        {
            Flags = flags;
        }

        private void Place(bool value, Flags flag)
        {
            if (value)
            {
                Set(flag);
            }
            else
            {
                Unset(flag);
            }
        }

        private void Set(Flags flags)
        {
            Flags |= flags;
        }

        private void Unset(Flags flags)
        {
            Flags = Flags & (~flags);
        }

        public bool IsSet(Flags flag)
        {
            return (Flags & flag) == flag;
        }

        private void HandleUnknownOpcode(byte opcode)
        {
            switch (OCErrorMode)
            {
                case OCErrorMode.ERROR:
                    throw new Exception(string.Format("unknown opcode: 0x{0:X}", opcode));
                case OCErrorMode.PRINT:
                    Console.WriteLine("unknown opcode: 0x{0:X}", opcode);
                    break;
                default:
                    break;
            }
        }

        public string FlagsToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("flags: ");
            sb.Append(string.Format("{0}", IsSet(Flags.ZERO) ? "Z" : "-"));
            sb.Append(string.Format("{0}", IsSet(Flags.SUB) ? "N" : "-"));
            sb.Append(string.Format("{0}", IsSet(Flags.HCARRY) ? "H" : "-"));
            sb.Append(string.Format("{0}", IsSet(Flags.CARRY) ? "C" : "-"));
            return sb.ToString();
        }
    }
}
