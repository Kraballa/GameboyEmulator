using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class CPU
    {
        Flags Flags = 0;
        Registers Regs;
        Memory Memory;
        Rom Rom;

        public CPU(Rom rom = null)
        {
            Regs = new Registers();
            Memory = new Memory();

            if (rom == null)
            {
                rom = Rom.Empty;
            }
            else
            {
                Rom = rom;
            }
            //TODO: load first rom bank into memory
        }

        private byte Fetch()
        {
            return Memory[Regs.PC++];
        }

        private void Execute(byte opcode)
        {
            uint HighBit = (uint)(opcode >> 4);

            switch (opcode)
            {
                #region Special Operations
                case 0x00: //NOP
                    //do nothing
                    break;
                case 0x10: //STOP
                    //TODO
                    break;

                case 0xCB:
                    Execute16BitOpcodes(Fetch());
                    break;
                #endregion

                #region LD XY, d16
                case 0x01:
                case 0x11:
                case 0x21:
                case 0x31:
                    Regs[HighBit + 1] = (ushort)(Fetch() | Fetch() << 8);
                    break;
                #endregion

                #region LD (XY), A
                case 0x02:
                case 0x12:
                    Memory[Regs[HighBit + 1]] = Regs.A;
                    break;
                case 0x22:
                    Memory[Regs.HL++] = Regs.A;
                    break;
                case 0x32:
                    Memory[Regs.HL--] = Regs.A;
                    break;
                #endregion

                #region INC XY
                case 0x03:
                case 0x13:
                case 0x23:
                case 0x33:
                    Regs[HighBit + 1]++;
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
                    break;
                case 0x34:
                    Memory[Regs.HL]++;
                    Unset(Flags.SUB);
                    if (Regs.B == 0)
                        Set(Flags.ZERO | Flags.HCARRY);
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
                    break;
                case 0x35:
                    if (Memory[Regs.HL] == 0)
                        Set(Flags.HCARRY);
                    Memory[Regs.HL]--;
                    Set(Flags.SUB);
                    if (Memory[Regs.HL] == 0)
                        Set(Flags.ZERO);
                    break;
                    #endregion
            }
        }

        private void Execute16BitOpcodes(byte opcode) //always prefixed with 'CB'
        {
            switch (opcode)
            {

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

        private bool IsSet(Flags flag)
        {
            return (Flags & flag) == flag;
        }
    }
}
