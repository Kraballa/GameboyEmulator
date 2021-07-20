using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum OCHandleMode
    {
        ERROR,
        PRINT,
        NOTHING
    }

    public class CPU
    {
        public OCHandleMode OCHandleMode = OCHandleMode.ERROR;

        Flags Flags = 0;
        Registers Regs;
        Memory Memory;
        Rom Rom;
        LCD LCD;

        public CPU(Rom rom = null)
        {
            Regs = new Registers();
            Memory = new Memory();
            LCD = new LCD(Memory);

            if (rom == null)
            {
                Rom = Rom.Empty;
            }
            else
            {
                Rom = rom;
            }
            //TODO: load first rom bank into memory
        }

        protected virtual byte Fetch()
        {
            return Memory[Regs.PC++];
        }

        protected virtual void Execute(byte opcode)
        {
            uint HighBit = (uint)(opcode >> 4);
            byte Data = 0;
            int Cycles = 0;

            switch (opcode)
            {
                #region Special Operations
                case 0x00: //NOP
                    //do nothing
                    Cycles = 1;
                    break;
                case 0x10: //STOP
                    //TODO
                    Fetch();
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
                    Execute16BitOpcodes(Fetch());
                    break;
                #endregion

                #region JR NX, s8
                case 0x20:
                    Cycles = 2;
                    if (IsSet(Flags.ZERO))
                    {
                        ushort instr = (ushort)(Regs.PC - 1);
                        Regs.PC = (ushort)(instr + Fetch());
                        Cycles++;
                    }
                    break;
                case 0x30:
                    Cycles = 2;
                    if (!IsSet(Flags.CARRY))
                    {
                        ushort instr = (ushort)(Regs.PC - 1);
                        Regs.PC = (ushort)(instr + Fetch());
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
                    if (Regs.GetHigh(HighBit + 1) == 0)
                        Set(Flags.HCARRY);
                    Regs.SetHigh(HighBit + 1, (byte)(Regs.GetHigh(HighBit + 1) - 1)); //--
                    Set(Flags.SUB);
                    if (Regs.GetHigh(HighBit + 1) == 0)
                        Set(Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x35:
                    if (Memory[Regs.HL] == 0)
                        Set(Flags.HCARRY);
                    Memory[Regs.HL]--;
                    Set(Flags.SUB);
                    if (Memory[Regs.HL] == 0)
                        Set(Flags.ZERO);
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

                default: //unknown opcode
                    switch (OCHandleMode)
                    {
                        case OCHandleMode.ERROR:
                            throw new Exception(string.Format("unknown opcode: {0:X}", opcode));
                        case OCHandleMode.PRINT:
                            Console.WriteLine("unknown opcode: {0:X}", opcode);
                            break;
                        default:
                            break;
                    }
                    break;
            }
        }

        private void Execute16BitOpcodes(byte opcode) //always prefixed with 'CB'
        {
            switch (opcode)
            {
                default: //unknown opcode
                    switch (OCHandleMode)
                    {
                        case OCHandleMode.ERROR:
                            throw new Exception(string.Format("unknown opcode: {0:X}", opcode));
                        case OCHandleMode.PRINT:
                            Console.WriteLine("unknown opcode: {0:X}", opcode);
                            break;
                        default:
                            break;
                    }
                    break;
            }
        }

        private void FlushFlags(Flags flags = 0)
        {
            Flags = flags;
        }

        private void Set(Flags flags)
        {
            Flags |= flags;
        }

        private void Unset(Flags flags)
        {
            Flags = Flags & (~flags);
        }

        private bool IsSet(Flags flag)
        {
            return (Flags & flag) == flag;
        }

        public void PrintDebugInfo()
        {
            Console.WriteLine("- Gameboy CPU debug info -");
            Console.Write("flags: ");
            Console.Write("{0}", IsSet(Flags.ZERO) ? "Z" : "-");
            Console.Write("{0}", IsSet(Flags.SUB) ? "N" : "-");
            Console.Write("{0}", IsSet(Flags.HCARRY) ? "H" : "-");
            Console.Write("{0}", IsSet(Flags.CARRY) ? "C" : "-");
            Console.WriteLine("----");
        }
    }
}
