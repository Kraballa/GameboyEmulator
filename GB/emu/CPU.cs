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
        Display LCD;
        Input Input;

        private int Clock = 0;

        public CPU(Rom rom)
        {
            Regs = new Registers();
            Memory = new Memory();
            LCD = new Display(Memory);
            Input = new Input(Memory);
            Rom = rom;
            //TODO: load first rom bank into memory
        }

        /// <summary>
        /// one frame, as in the period between 2 VBlanks
        /// 
        /// Occurs every 70224 clocks, VBlank last 4560.
        /// </summary>
        public void Step()
        {
            while (Clock < 70224)
            {
                //not accurate
                Clock += Execute(Fetch());
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

                case 0x27:
                    //TODO
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
                    if (Regs.HL + Regs[HighBit + 1] > ushort.MaxValue)
                        Set(Flags.CARRY | Flags.HCARRY);
                    else
                        Unset(Flags.CARRY | Flags.HCARRY);
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
            return Cycles;
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
