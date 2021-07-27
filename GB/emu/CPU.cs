using System;
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
        public static CPU Instance;

        public OCErrorMode OCErrorMode = OCErrorMode.ERROR;

        public Registers Regs;
        public Memory Memory;
        public Rom Rom;
        public Display LCD;
        public Input Input;

        protected CPUMode CPUMode = CPUMode.NORMAL;
        protected int Cycles = 0;

        public CPU(Rom rom)
        {
            Instance = this;

            Rom = rom;
            Regs = new Registers();
            Memory = new Memory(rom);
            LCD = new Display(Memory);
            Input = new Input(Memory);

            //skip starting sequence and jump straight to cartridge start
            Regs.PC = 0x100;
            //set registers to appropriate values
            Regs.AF = 0x01B0;
            Regs.BC = 0x0013;
            Regs.DE = 0x00D8;
            Regs.HL = 0x014D;
            Regs.SP = 0xFFFE;

            Memory.ReceiveSerialByte = PrintByte;
        }

        public virtual void Step()
        {
            while (Cycles < 70224)
            {
                int cycleDelta = Execute(Fetch()) * 4;
                Cycles += cycleDelta;
                LCD.UpdateGraphics(cycleDelta);
                HandleInterrupts();
            }
            Cycles -= 70224;
        }

        protected virtual byte Fetch()
        {
            return Memory[Regs.PC++];
        }

        protected ushort FetchWord()
        {
            return (ushort)(Fetch() | (Fetch() << 8));
        }

        protected virtual int Execute(byte opcode)
        {
            uint HighBit = (uint)(opcode >> 4);
            uint LowBit = (uint)(opcode & 0x0F);
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
                    Memory.IMEF = Memory.PrevIMEF;
                    Regs.PC = Memory.Pop();
                    Cycles = 4;
                    break;
                case 0xCB:
                    Cycles = Execute16Bit(Fetch());
                    break;
                case 0x2F:
                    Regs.A ^= 0b11111111; //flip all bits
                    Regs.Set(Flags.SUB | Flags.HCARRY);
                    Cycles = 1;
                    break;
                case 0x3F:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Regs.Flags ^= Flags.CARRY;
                    break;
                case 0x27: //DAA. see https://forums.nesdev.com/viewtopic.php?t=15944
                    if (!Regs.IsSet(Flags.SUB))
                    {
                        if (Regs.IsSet(Flags.CARRY) || Regs.A > 0x99)
                        {
                            Regs.A += 0x60;
                            Regs.Set(Flags.CARRY);
                        }
                        if (Regs.IsSet(Flags.HCARRY) || (Regs.A & 0x0F) > 0x09)
                        {
                            Regs.A += 0x6;
                        }
                    }
                    else
                    {
                        if (Regs.IsSet(Flags.CARRY))
                            Regs.A -= 0x60;
                        if (Regs.IsSet(Flags.HCARRY))
                            Regs.A -= 0x6;
                    }
                    Cycles = 1;
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Regs.Unset(Flags.HCARRY);
                    break;

                case 0x37:
                    Regs.FlushFlags(Flags.CARRY | (Regs.Flags & Flags.ZERO)); //corresponds to -001 i.e. ignore zero, set Carry, rest 0
                    Cycles = 1;
                    break;

                case 0x08:
                    ushort address = FetchWord();
                    Memory[address] = Regs.GetLow(4);
                    Memory[address] = Regs.GetHigh(4);
                    Cycles = 5;
                    break;
                #endregion

                #region JR NX, s8
                case 0x20:
                    if (!Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)((sbyte)Fetch() + Regs.PC);
                        Cycles = 3;
                    }
                    else
                    {
                        Regs.PC++;
                        Cycles = 2;
                    }
                    break;
                case 0x30:

                    if (!Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)((sbyte)Fetch() + Regs.PC);
                        Cycles = 3;
                    }
                    else
                    {
                        Regs.PC++;
                        Cycles = 2;
                    }
                    break;

                #endregion

                #region LD XY, d16
                case 0x01:
                case 0x11:
                case 0x21:
                case 0x31:
                    Regs[HighBit + 1] = FetchWord();
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
                    Regs.CheckHCarry(Regs.GetHigh(HighBit + 1), 1);
                    Regs.SetHigh(HighBit + 1, (byte)(Regs.GetHigh(HighBit + 1) + 1)); //++
                    Regs.Unset(Flags.SUB);
                    Regs.Place(Regs.GetHigh(HighBit + 1) == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x34:
                    Regs.CheckHCarry(Memory[Regs.HL], 1);
                    Memory[Regs.HL]++;
                    Regs.Unset(Flags.SUB);
                    Regs.Set(Flags.ZERO);
                    Cycles = 3;
                    break;
                #endregion

                #region DEC X
                case 0x05:
                case 0x15:
                case 0x25:
                    Regs.CheckHCarryDec(Regs.GetHigh(HighBit + 1), 1);
                    Regs.SetHigh(HighBit + 1, (byte)(Regs.GetHigh(HighBit + 1) - 1)); //--
                    Regs.Set(Flags.SUB);
                    Regs.Place(Regs.GetHigh(HighBit + 1) == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x35:
                    Regs.CheckHCarryDec(Memory[Regs.HL], 1);
                    Memory[Regs.HL]--;
                    Regs.Set(Flags.SUB);
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
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
                    Regs.FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A7 to CY
                    Cycles = 1;
                    break;
                case 0x17:
                    Data = (byte)(Regs.A >> 7);
                    Regs.A <<= 1;
                    Regs.A |= (byte)(Regs.IsSet(Flags.CARRY) ? 1 : 0); // write CY to A0
                    Regs.FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A7 to CY
                    Cycles = 1;
                    break;
                #endregion

                #region JR [0|Z|C], s8
                case 0x18:
                    Regs.PC = (ushort)((sbyte)Fetch() + Regs.PC);
                    Cycles = 3;
                    break;
                case 0x28:

                    if (Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)((sbyte)Fetch() + Regs.PC);
                        Cycles = 3;
                    }
                    else
                    {
                        Regs.PC++;
                        Cycles = 2;
                    }
                    break;
                case 0x38:

                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)((sbyte)Fetch() + Regs.PC);
                        Cycles = 3;
                    }
                    else
                    {
                        Regs.PC++;
                        Cycles = 2;
                    }
                    break;
                #endregion

                #region ADD HL, XY
                case 0x09:
                case 0x19:
                case 0x29:
                case 0x39:
                    Regs.CheckHCarry(Regs.HL, Regs[HighBit + 1]);
                    Regs.Place((uint)Regs.HL + Regs[HighBit + 1] > ushort.MaxValue, Flags.CARRY);
                    Regs.HL += Regs[HighBit + 1];
                    Regs.Unset(Flags.SUB);
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
                    Regs.CheckHCarry(Regs.GetLow(HighBit + 1), 1);
                    Regs.SetLow(HighBit + 1, (byte)(Regs.GetLow(HighBit + 1) + 1)); //++
                    Regs.Unset(Flags.SUB);
                    Regs.Place(Regs.GetLow(HighBit + 1) == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x3C:
                    Regs.CheckHCarry(Regs.A, 1);
                    Regs.A++;
                    Regs.Unset(Flags.SUB);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                #endregion

                #region DEC Y
                case 0x0D:
                case 0x1D:
                case 0x2D:
                    Regs.CheckHCarryDec(Regs.GetLow(HighBit + 1), 1);
                    Regs.SetLow(HighBit + 1, (byte)(Regs.GetLow(HighBit + 1) - 1)); //--
                    Regs.Set(Flags.SUB);
                    Regs.Place(Regs.GetLow(HighBit + 1) == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x3D:
                    Regs.CheckHCarryDec(Regs.A, 1);
                    Regs.A--;
                    Regs.Set(Flags.SUB);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
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
                    Regs.FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A0 to CY
                    Cycles = 1;
                    break;
                case 0x1F:
                    Data = (byte)(Regs.A & 0x1);
                    Regs.A >>= 1;
                    Regs.FlushFlags(Data != 0 ? Flags.CARRY : 0); //write A0 to CY
                    Cycles = 1;
                    break;
                #endregion

                #region LD [B|D|H], [B|C|D|E|H|L]
                case 0x40:
                case 0x50:
                case 0x60:
                case 0x41:
                case 0x51:
                case 0x61:
                case 0x42:
                case 0x52:
                case 0x62:
                case 0x43:
                case 0x53:
                case 0x63:
                case 0x44:
                case 0x54:
                case 0x64:
                case 0x45:
                case 0x55:
                case 0x65:
                    Regs.SetHigh(HighBit - 3, Regs.GetByte(LowBit + 1));
                    Cycles = 1;
                    break;
                #endregion

                #region LD (HL), X
                case 0x70:
                case 0x71:
                case 0x72:
                case 0x73:
                case 0x74:
                case 0x75:
                    Memory[Regs.HL] = Regs.GetByte(LowBit + 1);
                    Cycles = 1;
                    break;
                #endregion

                #region LD X, (HL)
                case 0x46:
                case 0x56:
                case 0x66:
                    Regs.SetHigh(HighBit - 3, Memory[Regs.HL]);
                    Cycles = 2;
                    break;
                #endregion

                #region LD X, A
                case 0x47:
                case 0x57:
                case 0x67:
                    Regs.SetHigh(HighBit - 3, Regs.A);
                    Cycles = 1;
                    break;
                case 0x77:
                    Memory[Regs.HL] = Regs.A;
                    Cycles = 2;
                    break;
                #endregion

                #region LD [C|E|L], [B|C|D|E|H|L]
                case 0x48:
                case 0x58:
                case 0x68:
                case 0x49:
                case 0x59:
                case 0x69:
                case 0x4A:
                case 0x5A:
                case 0x6A:
                case 0x4B:
                case 0x5B:
                case 0x6B:
                case 0x4C:
                case 0x5C:
                case 0x6C:
                case 0x4D:
                case 0x5D:
                case 0x6D:
                    Regs.SetLow(HighBit - 3, Regs.GetByte(LowBit + 1));
                    Cycles = 1;
                    break;
                #endregion

                #region LD A, X
                case 0x78:
                case 0x79:
                case 0x7A:
                case 0x7B:
                case 0x7C:
                case 0x7D:
                    Regs.A = Regs.GetByte(LowBit + 1);
                    Cycles = 1;
                    break;
                #endregion

                #region LD [C|E|L] [(HL)|A]

                case 0x4E:
                case 0x5E:
                case 0x6E:
                    Regs.SetLow(HighBit - 3, Memory[Regs.HL]);
                    Cycles = 2;
                    break;
                case 0x7E:
                    Regs.A = Memory[Regs.HL];
                    Cycles = 2;
                    break;

                case 0x4F:
                case 0x5F:
                case 0x6F:
                    Regs.SetLow(HighBit - 3, Regs.A);
                    Cycles = 1;
                    break;
                case 0x7F:
                    Cycles = 1;
                    break;
                #endregion

                #region ADD A, [B|C|D|E|H|L]
                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                case 0x84:
                case 0x85:
                    {
                        Regs.Unset(Flags.SUB);
                        Regs.CheckHCarry(Regs.A, Regs.GetByte(LowBit + 1));
                        int val = Regs.GetByte(LowBit + 1);
                        Regs.Place(Regs.A + val > ushort.MaxValue, Flags.CARRY);
                        Regs.A += (byte)val;
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;

                case 0x86:
                    {
                        Regs.CheckHCarry(Regs.A, Memory[Regs.HL]);
                        int val = Regs.A + Memory[Regs.HL];
                        Regs.A += Memory[Regs.HL];
                        Regs.Place(val > byte.MaxValue, Flags.CARRY);
                        Regs.Unset(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                #endregion

                #region SUB A, [B|C|D|E|H|L]
                case 0x90:
                case 0x91:
                case 0x92:
                case 0x93:
                case 0x94:
                case 0x95:
                    {
                        Regs.Set(Flags.SUB);
                        int val = Regs.GetByte(LowBit + 1);
                        Regs.CheckHCarry(Regs.A, (byte)val);
                        Regs.Place(Regs.A + val > byte.MaxValue, Flags.CARRY);
                        Regs.CheckHCarry(Regs.A, (byte)val);
                        Regs.A += (byte)val;
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;
                case 0x96:
                    {
                        Regs.CheckHCarryDec(Regs.A, Memory[Regs.HL]);
                        int val = Regs.A - Memory[Regs.HL];
                        Regs.A -= Memory[Regs.HL];
                        Regs.Place(val < 0, Flags.CARRY);
                        Regs.Set(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                #endregion

                #region AND [B|C|D|E|H|L]
                case 0xA0:
                case 0xA1:
                case 0xA2:
                case 0xA3:
                case 0xA4:
                case 0xA5:
                    Regs.A &= Regs.GetByte(LowBit + 1);
                    Regs.Set(Flags.HCARRY);
                    Regs.Unset(Flags.SUB | Flags.CARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xA6:
                    Regs.A &= Memory[Regs.HL];
                    Regs.Set(Flags.HCARRY);
                    Regs.Unset(Flags.SUB | Flags.CARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                #endregion

                #region OR [B|C|D|E|H|L]
                case 0xB0:
                case 0xB1:
                case 0xB2:
                case 0xB3:
                case 0xB4:
                case 0xB5:
                    Regs.A |= Regs.GetByte(LowBit + 1);
                    Regs.Unset(Flags.SUB | Flags.HCARRY | Flags.CARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xB6:
                    Regs.A |= Memory[Regs.HL];
                    Regs.Unset(Flags.SUB | Flags.HCARRY | Flags.CARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                #endregion

                #region things with A
                case 0x87:
                    Regs.CheckHCarry(Regs.A, Regs.A);
                    Regs.Place(Regs.A > 0x0F, Flags.CARRY);
                    Regs.A += Regs.A;
                    Regs.Unset(Flags.SUB);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x97:
                    Regs.CheckHCarryDec(Regs.A, Regs.A);
                    Regs.A = 0;
                    Regs.Set(Flags.SUB | Flags.ZERO);
                    Regs.Unset(Flags.CARRY);
                    Cycles = 1;
                    break;
                case 0xA7:
                    Regs.FlushFlags(Flags.HCARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xB7:
                    Regs.FlushFlags(0);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;

                #endregion

                #region ADC A, [B|C|D|E|H|L]
                case 0x88:
                case 0x89:
                case 0x8A:
                case 0x8B:
                case 0x8C:
                case 0x8D:
                    {
                        Regs.Unset(Flags.SUB);
                        int val = Regs.GetByte(LowBit + 1);
                        val += Regs.IsSet(Flags.CARRY) ? 1 : 0;
                        Regs.CheckHCarry(Regs.A, (byte)val); //TODO: this might break if val is >0xFF
                        Regs.Place(Regs.A + val > ushort.MaxValue, Flags.CARRY);
                        Regs.A += (byte)val;
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;

                case 0x8E:
                    {
                        int val = Memory[Regs.HL];
                        val += Regs.IsSet(Flags.CARRY) ? 1 : 0;
                        Regs.CheckHCarry(Regs.A, (byte)val); //TODO: this might break if val is >0xFF
                        Regs.Place(Regs.A + val > byte.MaxValue, Flags.CARRY);
                        Regs.A += Memory[Regs.HL];
                        Regs.Unset(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                #endregion

                #region SBC A, [B|C|D|E|H|L]
                case 0x98:
                case 0x99:
                case 0x9A:
                case 0x9B:
                case 0x9C:
                case 0x9D:
                    {
                        Regs.Set(Flags.SUB);
                        int val = Regs.GetByte(LowBit + 1);
                        val += Regs.IsSet(Flags.CARRY) ? 1 : 0;
                        Regs.CheckHCarryDec(Regs.A, (byte)val); //TODO: might break if register was already > 0xFF
                        Regs.Place(Regs.A - val < 0, Flags.CARRY);
                        Regs.A += (byte)val;
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;
                case 0x9E:
                    {
                        int val = Regs.A - Memory[Regs.HL];
                        val += Regs.IsSet(Flags.CARRY) ? 1 : 0;
                        Regs.CheckHCarryDec(Regs.A, Memory[Regs.HL]); //TODO: might break if register was already > 0xFF
                        Regs.A -= Memory[Regs.HL];
                        Regs.Place(val < 0, Flags.CARRY);
                        Regs.Set(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                #endregion

                #region XOR [B|C|D|E|H|L]
                case 0xA8:
                case 0xA9:
                case 0xAA:
                case 0xAB:
                case 0xAC:
                case 0xAD:
                    Regs.A ^= Regs.GetByte(LowBit - 7);
                    Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xAE:
                    Regs.A ^= Memory[Regs.HL];
                    Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                #endregion

                #region CP [B|C|D|E|H|L]
                case 0xB8:
                case 0xB9:
                case 0xBA:
                case 0xBB:
                case 0xBC:
                case 0xBD:
                    Regs.CheckHCarryDec(Regs.A, Regs.GetByte(LowBit - 7)); //TODO: figure out if this is correct
                    Regs.Place(Regs.A == Regs.GetByte(LowBit - 7), Flags.ZERO);
                    Regs.Set(Flags.SUB);
                    Regs.Place(Regs.A < Regs.GetByte(LowBit - 7), Flags.CARRY);
                    Cycles = 1;
                    break;
                case 0xBE:
                    Regs.Place(Regs.A == Memory[Regs.HL], Flags.ZERO);
                    Regs.Place(Regs.A < Memory[Regs.HL], Flags.CARRY);
                    Cycles = 2;
                    break;
                #endregion

                #region More A shenanigans
                case 0x8F:
                    {
                        int val = Regs.A;
                        val += Regs.IsSet(Flags.CARRY) ? 1 : 0;
                        Regs.CheckHCarry(Regs.A, (byte)val);
                        Regs.Place(Regs.A + val > byte.MaxValue, Flags.CARRY);
                        Regs.Unset(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;
                case 0x9F:
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Regs.CheckHCarryDec(0, 1);
                        Regs.A = 0xFF;
                    }
                    else
                    {
                        Regs.A = 0;
                        Regs.Unset(Flags.HCARRY | Flags.CARRY);
                    }
                    Regs.Set(Flags.SUB);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xAF:
                    Regs.A = 0;
                    Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Regs.Set(Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xBF:
                    Regs.Set(Flags.ZERO | Flags.SUB);
                    Regs.Unset(Flags.HCARRY | Flags.CARRY);
                    Cycles = 1;
                    break;
                #endregion

                case 0xC0:
                    Cycles = 2;
                    if (!Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = Memory.Pop();
                        Cycles += 3;
                    }
                    break;

                case 0xD0:
                    Cycles = 2;
                    if (!Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = Memory.Pop();
                        Cycles += 3;
                    }
                    break;

                case 0xE0:
                    Memory[(ushort)(Fetch() | 0xFF00)] = Regs.A;
                    Cycles = 3;
                    break;

                case 0xF0:
                    Regs.A = Memory[(ushort)(Fetch() | 0xFF00)];
                    break;

                #region POP [BC|DE|HL|AF]
                case 0xC1:
                case 0xD1:
                case 0xE1:
                    Regs[HighBit - 0xB] = Memory.Pop();
                    Cycles = 3;
                    break;
                case 0xF1:
                    Regs[0] = Memory.Pop();
                    Cycles = 3;
                    break;
                #endregion

                case 0xC2:
                    if (!Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = FetchWord();
                        Cycles = 4; ;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;
                case 0xD2:
                    if (!Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = FetchWord();
                        Cycles = 4; ;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;

                case 0xE2:
                    Memory[(ushort)(Regs.C | 0xFF00)] = Regs.A;
                    Cycles = 2;
                    break;
                case 0xF2:
                    Regs.A = Memory[(ushort)(Regs.C | 0xFF00)];
                    Cycles = 2;
                    break;

                case 0xC3:
                    Regs.PC = FetchWord();
                    Cycles = 4;
                    break;

                case 0xC4:
                    if (!Regs.IsSet(Flags.ZERO))
                    {
                        Memory.Push(Regs.PC);
                        Regs.PC = FetchWord();
                        Cycles = 6;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;
                case 0xD4:
                    if (!Regs.IsSet(Flags.CARRY))
                    {
                        Memory.Push(Regs.PC);
                        Regs.PC = FetchWord();
                        Cycles = 6;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;

                case 0xC5:
                case 0xD5:
                case 0xE5:
                    Memory.Push(Regs[HighBit - 0xB]);
                    Cycles = 4;
                    break;
                case 0xF5:
                    Memory.Push(Regs.AF);
                    Cycles = 4;
                    break;

                #region ADD|SUB|AND|OR A immediate
                case 0xC6:
                    {
                        Regs.Unset(Flags.SUB);
                        byte val = Fetch();
                        Regs.CheckHCarry(Regs.A, val);
                        Regs.Place(Regs.A + val > byte.MaxValue, Flags.CARRY);
                        Regs.A += val;
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                case 0xD6:
                    {
                        Regs.Set(Flags.SUB);
                        int val = Fetch();
                        Regs.CheckHCarryDec(Regs.A, (byte)val);
                        Regs.Place(Regs.A - val < 0, Flags.CARRY);
                        Regs.A -= (byte)val;
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                case 0xE6:
                    Regs.A &= Fetch();
                    Regs.Set(Flags.HCARRY);

                    Regs.Unset(Flags.SUB | Flags.CARRY);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0xF6:
                    Regs.A |= Fetch();
                    Regs.FlushFlags(0);
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                #endregion

                #region RST [0-7]
                case 0xC7:
                case 0xD7:
                case 0xE7:
                case 0xF7:
                    Memory.Push(Regs.PC);
                    Regs.PC = (ushort)((HighBit - 0xC) * 0x10);
                    Cycles = 4;
                    break;
                case 0xCF:
                case 0xDF:
                case 0xEF:
                case 0xFF:
                    Memory.Push(Regs.PC);
                    Regs.PC = (ushort)((HighBit - 0xC) * 0x10 + 8);
                    Cycles = 4;
                    break;
                #endregion

                case 0xC8:
                    if (Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = Memory.Pop();
                        Cycles = 5;
                    }
                    else
                    {
                        Cycles = 2;
                    }
                    break;
                case 0xD8:
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = Memory.Pop();
                        Cycles = 5;
                    }
                    else
                    {
                        Cycles = 2;
                    }
                    break;

                case 0xE8:
                    {
                        int value = (sbyte)Fetch();
                        Regs.CheckHCarry(Regs.SP, (ushort)value);
                        Regs.Place(value > ushort.MaxValue, Flags.CARRY);
                        Regs.SP += (ushort)value;
                        Regs.Unset(Flags.ZERO | Flags.SUB);
                        Cycles = 4;
                    }
                    break;

                case 0xF8:
                    {
                        int value = (sbyte)Fetch() + Regs.SP;
                        Regs.CheckHCarry(Regs.HL, (ushort)value);
                        Regs.Place(value > ushort.MaxValue, Flags.CARRY);
                        Regs.HL += (ushort)value;
                        Cycles = 3;
                    }
                    break;

                case 0xC9:
                    Regs.PC = Memory.Pop();
                    Cycles = 4;
                    break;

                case 0xE9:
                    Regs.PC = Regs.HL;
                    Cycles = 1;
                    break;
                case 0xF9:
                    Regs.SP = Regs.HL;
                    Cycles = 2;
                    break;

                #region JP C,Z
                case 0xCA:
                    if (Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = FetchWord();
                        Cycles = 4;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;
                case 0xDA:
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = FetchWord();
                        Cycles = 4;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;
                #endregion

                #region LD A (16)
                case 0xEA:
                    Memory[FetchWord()] = Regs.A;
                    Cycles = 4;
                    break;
                case 0xFA:
                    Regs.A = Memory[FetchWord()];
                    Cycles = 4;
                    break;
                #endregion

                #region CALL C,Z
                case 0xCC:
                    if (Regs.IsSet(Flags.ZERO))
                    {
                        Regs.PC = FetchWord();
                        Cycles = 6;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;
                case 0xDC:
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Regs.PC = FetchWord();
                        Cycles = 6;
                    }
                    else
                    {
                        Regs.PC += 2;
                        Cycles = 3;
                    }
                    break;
                case 0xCD:
                    Regs.PC = FetchWord();
                    Cycles = 6;
                    break;
                #endregion

                #region Even more A shenanigans
                case 0xCE:
                    {
                        int value = Fetch() + (Regs.IsSet(Flags.CARRY) ? 1 : 0);
                        Regs.CheckHCarry(Regs.A, (byte)value);
                        Regs.Place(value > byte.MaxValue, Flags.CARRY);
                        Regs.A += (byte)value;
                        Regs.Unset(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                case 0xDE:
                    {
                        int value = Fetch() + (Regs.IsSet(Flags.CARRY) ? 1 : 0);
                        Regs.CheckHCarryDec(Regs.A, (byte)value);
                        Regs.Place(value < 0, Flags.CARRY);
                        Regs.A -= (byte)value;
                        Regs.Set(Flags.SUB);
                        Regs.Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                case 0xEE:
                    Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Regs.A ^= Fetch();
                    Regs.Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0xFE:
                    {
                        int value = Fetch();
                        Regs.Set(Flags.SUB);
                        Regs.CheckHCarryDec(Regs.A, (byte)value);
                        Regs.Place(Regs.A - value < 0, Flags.CARRY);
                        Regs.Place(Regs.A == value, Flags.ZERO);
                        Cycles = 2;
                    }
                    break;
                #endregion

                #region Undefined Opcodes
                case 0xD3:
                case 0xDB:
                case 0xDD:
                case 0xE3:
                case 0xE4:
                case 0xEB:
                case 0xEC:
                case 0xED:
                case 0xF4:
                case 0xFC:
                case 0xFD:
                    HandleInvalidOpcode(opcode);
                    break;
                    #endregion
            }
            return Cycles;
        }

        protected virtual int Execute16Bit(byte opcode) //always prefixed with 'CB'
        {
            uint HighBit = (uint)(opcode >> 4);
            uint LowBit = (uint)(opcode & 0x0F);
            byte Data;
            int Cycles = 0;
            switch (opcode)
            {
                #region RLC
                case 0x00:
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                case 0x05:
                case 0x07:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Regs.GetByte(LowBit + 1);
                    Regs.SetByte(LowBit + 1, (byte)((Data << 1) | (Data >> 7)));
                    Regs.Place((Data & 0b10000000) != 0, Flags.CARRY);
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x06:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Memory[Regs.HL];
                    Memory[Regs.HL] = (byte)((Data << 1) | (Data >> 7));
                    Regs.Place((Data & 0b10000000) != 0, Flags.CARRY);
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region RL
                case 0x10:
                case 0x11:
                case 0x12:
                case 0x13:
                case 0x14:
                case 0x15:
                case 0x17:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = (byte)(Regs.GetByte(LowBit + 1) << 1);
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Data |= 0b00000001;
                    }
                    else
                    {
                        Data &= 0b11111110;
                    }
                    Regs.Place((Data & 0b10000000) == 1, Flags.CARRY);
                    Regs.SetByte(LowBit + 1, Data);
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x16:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = (byte)(Memory[Regs.HL] << 1);
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Data |= 0b00000001;
                    }
                    else
                    {
                        Data &= 0b11111110;
                    }
                    Regs.Place((Data & 0b10000000) == 1, Flags.CARRY);
                    Memory[Regs.HL] = (byte)((Data << 1) | (Data >> 7));
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region SLA
                case 0x20:
                case 0x21:
                case 0x22:
                case 0x23:
                case 0x24:
                case 0x25:
                case 0x27:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Regs.GetByte(LowBit + 1);
                    Regs.Place((Data & 0x10000000) == 1, Flags.CARRY);
                    Regs.SetByte(LowBit + 1, (byte)(Data << 1));
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x26:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Memory[Regs.HL];
                    Regs.Place((Data & 0x10000000) == 1, Flags.CARRY);
                    Memory[Regs.HL] = (byte)(Data << 1);
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region SWAP
                case 0x30:
                case 0x31:
                case 0x32:
                case 0x33:
                case 0x34:
                case 0x35:
                case 0x37:
                    Data = Regs.GetByte(LowBit + 1);
                    Data = (byte)(Data << 4 | Data >> 4);
                    Regs.SetByte(LowBit + 1, Data);
                    Regs.Place(Data == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x36:
                    Data = Memory[Regs.HL];
                    Data = (byte)(Data << 4 | Data >> 4);
                    Memory[Regs.HL] = Data;
                    Regs.Place(Data == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region RRC
                case 0x08:
                case 0x09:
                case 0x0A:
                case 0x0B:
                case 0x0C:
                case 0x0D:
                case 0x0F:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Regs.GetByte(LowBit + 1);
                    Regs.Place((Data & 0b00000001) != 0, Flags.CARRY);
                    Regs.SetByte(LowBit + 1, (byte)((Data >> 1) | (Data << 7)));
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x0E:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Memory[Regs.HL];
                    Regs.Place((Data & 0b00000001) != 0, Flags.CARRY);
                    Memory[Regs.HL] = (byte)((Data >> 1) | (Data << 7));
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region RR
                case 0x18:
                case 0x19:
                case 0x1A:
                case 0x1B:
                case 0x1C:
                case 0x1D:
                case 0x1F:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = (byte)(Regs.GetByte(LowBit + 1) >> 1);
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Data |= 0b10000000;
                    }
                    else
                    {
                        Data &= 0b01111111;
                    }
                    Regs.Place((Data & 0x00000001) == 1, Flags.CARRY);
                    Regs.SetByte(LowBit + 1, (byte)((Data >> 1) | (Data << 7)));
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x1E:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = (byte)(Memory[Regs.HL] >> 1);
                    if (Regs.IsSet(Flags.CARRY))
                    {
                        Data |= 0b10000000;
                    }
                    else
                    {
                        Data &= 0b01111111;
                    }
                    Regs.Place((Data & 0x00000001) == 1, Flags.CARRY);
                    Memory[Regs.HL] = (byte)((Data >> 1) | (Data << 7));
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region SRA
                case 0x28:
                case 0x29:
                case 0x2A:
                case 0x2B:
                case 0x2C:
                case 0x2D:
                case 0x2F:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Regs.GetByte(LowBit + 1);
                    Regs.Place((Data & 0x00000001) == 1, Flags.CARRY);
                    Regs.SetByte(LowBit + 1, (byte)(Data >> 1));
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x2E:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Memory[Regs.HL];
                    Regs.Place((Data & 0x00000001) == 1, Flags.CARRY);
                    Memory[Regs.HL] = (byte)(Data >> 1);
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region SRL
                case 0x38:
                case 0x39:
                case 0x3A:
                case 0x3B:
                case 0x3C:
                case 0x3D:
                case 0x3F:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Regs.GetByte(LowBit + 1);
                    Regs.Place((Data & 0x00000001) == 1, Flags.CARRY);
                    Regs.SetByte(LowBit + 1, (byte)((Data >> 1) & 0b01111111));
                    Regs.Place(Regs.GetByte(LowBit + 1) == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                case 0x3E:
                    Regs.Unset(Flags.SUB | Flags.HCARRY);
                    Data = Memory[Regs.HL];
                    Regs.Place((Data & 0x00000001) == 1, Flags.CARRY);
                    Memory[Regs.HL] = (byte)((Data >> 1) & 0b01111111);
                    Regs.Place(Memory[Regs.HL] == 0, Flags.ZERO);
                    Cycles = 4;
                    break;
                #endregion

                #region Bit Set
                case 0x40:
                case 0x41:
                case 0x42:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x47:
                case 0x50:
                case 0x51:
                case 0x52:
                case 0x53:
                case 0x54:
                case 0x55:
                case 0x57:
                case 0x60:
                case 0x61:
                case 0x62:
                case 0x63:
                case 0x64:
                case 0x65:
                case 0x67:
                case 0x70:
                case 0x71:
                case 0x72:
                case 0x73:
                case 0x74:
                case 0x75:
                case 0x77:
                    {
                        Data = (byte)(Regs.GetByte(LowBit + 1) & (1 << (((int)HighBit - 4) * 2)));
                        Regs.Place(Data != 0, Flags.ZERO);
                        Regs.Set(Flags.HCARRY);
                        Regs.Unset(Flags.SUB);
                        Cycles = 2;
                    }
                    break;

                case 0x48:
                case 0x49:
                case 0x4A:
                case 0x4B:
                case 0x4C:
                case 0x4D:
                case 0x4F:
                case 0x58:
                case 0x59:
                case 0x5A:
                case 0x5B:
                case 0x5C:
                case 0x5D:
                case 0x5F:
                case 0x68:
                case 0x69:
                case 0x6A:
                case 0x6B:
                case 0x6C:
                case 0x6D:
                case 0x6F:
                case 0x78:
                case 0x79:
                case 0x7A:
                case 0x7B:
                case 0x7C:
                case 0x7D:
                case 0x7F:
                    {
                        Data = (byte)(Regs.GetByte(LowBit - 7) & (1 << (((int)HighBit - 4) * 2 + 1)));
                        Regs.Place(Data != 0, Flags.ZERO);
                        Regs.Set(Flags.HCARRY);
                        Regs.Unset(Flags.SUB);
                        Cycles = 2;
                    }
                    break;

                case 0x46:
                case 0x56:
                case 0x66:
                case 0x76:
                    {
                        Data = (byte)(Memory[Regs.HL] & (1 << (((int)HighBit - 4) * 2)));
                        Regs.Place(Data != 0, Flags.ZERO);
                        Regs.Set(Flags.HCARRY);
                        Regs.Unset(Flags.SUB);
                        Cycles = 3;
                    }
                    break;

                case 0x4E:
                case 0x5E:
                case 0x6E:
                case 0x7E:
                    {
                        Data = (byte)(Memory[Regs.HL] & (1 << (((int)HighBit - 4) * 2 + 1)));
                        Regs.Place(Data != 0, Flags.ZERO);
                        Regs.Set(Flags.HCARRY);
                        Regs.Unset(Flags.SUB);
                        Cycles = 3;
                    }
                    break;
                #endregion

                #region Reset
                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                case 0x84:
                case 0x85:
                case 0x87:
                case 0x90:
                case 0x91:
                case 0x92:
                case 0x93:
                case 0x94:
                case 0x95:
                case 0x97:
                case 0xA0:
                case 0xA1:
                case 0xA2:
                case 0xA3:
                case 0xA4:
                case 0xA5:
                case 0xA7:
                case 0xB0:
                case 0xB1:
                case 0xB2:
                case 0xB3:
                case 0xB4:
                case 0xB5:
                case 0xB7:
                    {
                        Data = (byte)(Regs.GetByte(LowBit + 1) & ~(1 << (((int)HighBit - 8) * 2)));
                        Regs.SetByte(LowBit + 1, Data);
                        Cycles = 2;
                    }
                    break;
                case 0x86:
                case 0x96:
                case 0xA6:
                case 0xB6:
                    {
                        Data = (byte)(Memory[Regs.HL] & ~(1 << (((int)HighBit - 8) * 2)));
                        Memory[Regs.HL] = Data;
                        Cycles = 4;
                    }
                    break;

                case 0x88:
                case 0x89:
                case 0x8A:
                case 0x8B:
                case 0x8C:
                case 0x8D:
                case 0x8F:
                case 0x98:
                case 0x99:
                case 0x9A:
                case 0x9B:
                case 0x9C:
                case 0x9D:
                case 0x9F:
                case 0xA8:
                case 0xA9:
                case 0xAA:
                case 0xAB:
                case 0xAC:
                case 0xAD:
                case 0xAF:
                case 0xB8:
                case 0xB9:
                case 0xBA:
                case 0xBB:
                case 0xBC:
                case 0xBD:
                case 0xBF:
                    {
                        Data = (byte)(Regs.GetByte(LowBit + 1) & ~(1 << (((int)HighBit - 8) * 2 + 1)));
                        Regs.SetByte(LowBit + 1, Data);
                        Cycles = 2;
                    }
                    break;
                case 0x8E:
                case 0x9E:
                case 0xAE:
                case 0xBE:
                    {
                        Data = (byte)(Memory[Regs.HL] & ~(1 << (((int)HighBit - 8) * 2 + 1)));
                        Memory[Regs.HL] = Data;
                        Cycles = 4;
                    }
                    break;
                #endregion

                #region Set
                case 0xC0:
                case 0xC1:
                case 0xC2:
                case 0xC3:
                case 0xC4:
                case 0xC5:
                case 0xC7:
                case 0xD0:
                case 0xD1:
                case 0xD2:
                case 0xD3:
                case 0xD4:
                case 0xD5:
                case 0xD7:
                case 0xE0:
                case 0xE1:
                case 0xE2:
                case 0xE3:
                case 0xE4:
                case 0xE5:
                case 0xE7:
                case 0xF0:
                case 0xF1:
                case 0xF2:
                case 0xF3:
                case 0xF4:
                case 0xF5:
                case 0xF7:
                    {
                        Data = (byte)(Regs.GetByte(LowBit + 1) | (1 << (((int)HighBit - 8) * 2)));
                        Regs.SetByte(LowBit + 1, Data);
                        Cycles = 2;
                    }
                    break;
                case 0xC6:
                case 0xD6:
                case 0xE6:
                case 0xF6:
                    {
                        Data = (byte)(Memory[Regs.HL] | (1 << (((int)HighBit - 8) * 2)));
                        Memory[Regs.HL] = Data;
                        Cycles = 4;
                    }
                    break;

                case 0xC8:
                case 0xC9:
                case 0xCA:
                case 0xCB:
                case 0xCC:
                case 0xCD:
                case 0xCF:
                case 0xD8:
                case 0xD9:
                case 0xDA:
                case 0xDB:
                case 0xDC:
                case 0xDD:
                case 0xDF:
                case 0xE8:
                case 0xE9:
                case 0xEA:
                case 0xEB:
                case 0xEC:
                case 0xED:
                case 0xEF:
                case 0xF8:
                case 0xF9:
                case 0xFA:
                case 0xFB:
                case 0xFC:
                case 0xFD:
                case 0xFF:
                    {
                        Data = (byte)(Regs.GetByte(LowBit + 1) | (1 << (((int)HighBit - 8) * 2 + 1)));
                        Regs.SetByte(LowBit + 1, Data);
                        Cycles = 2;
                    }
                    break;
                case 0xCE:
                case 0xDE:
                case 0xEE:
                case 0xFE:
                    {
                        Data = (byte)(Memory[Regs.HL] | (1 << (((int)HighBit - 8) * 2 + 1)));
                        Memory[Regs.HL] = Data;
                        Cycles = 4;
                    }
                    break;
                    #endregion
            }
            return Cycles;
        }

        public void PrintByte(byte data)
        {
            Console.Write((char)data);
        }

        protected void HandleInvalidOpcode(byte opcode)
        {
            switch (OCErrorMode)
            {
                case OCErrorMode.ERROR:
                    throw new Exception(string.Format("invalid opcode: 0x{0:X}", opcode));
                case OCErrorMode.PRINT:
                    Console.WriteLine("invalid opcode: 0x{0:X}", opcode);
                    break;
                default:
                    break;
            }
        }

        public void RequestInterrupt(InterruptType type)
        {
            Memory[Memory.IFREG] |= (byte)type;
        }

        private void HandleInterrupts()
        {
            if (Memory.IMEF)
            {
                byte req = Memory[Memory.IFREG];
                byte enabled = Memory[Memory.IEREG];
                if (req > 0)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if ((req & (1 << i)) != 0 && (enabled & (1 << i)) != 0)
                        {
                            DoInterrupt((InterruptType)i);
                        }
                    }
                }
            }
        }

        private void DoInterrupt(InterruptType type)
        {
            Console.WriteLine("interrupting: {0}", type);
            Memory.PrevIMEF = Memory.IMEF;
            Memory.IMEF = false;
            Memory[Memory.IFREG] &= (byte)~(1 << (int)type);
            Memory.Push(Regs.PC);

            switch (type)
            {
                case InterruptType.VBLANK:
                    Regs.PC = 0x40;
                    break;
                case InterruptType.LCD:
                    Regs.PC = 0x48;
                    break;
                case InterruptType.TIMER:
                    Regs.PC = 0x50;
                    break;
                case InterruptType.SERIAL:
                    Regs.PC = 0x58;
                    break;
                case InterruptType.JOYPAD:
                    Regs.PC = 0x60;
                    break;
            }
        }

        public string FlagsToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("flags: ");
            sb.Append(string.Format("{0}", Regs.IsSet(Flags.ZERO) ? "Z" : "-"));
            sb.Append(string.Format("{0}", Regs.IsSet(Flags.SUB) ? "N" : "-"));
            sb.Append(string.Format("{0}", Regs.IsSet(Flags.HCARRY) ? "H" : "-"));
            sb.Append(string.Format("{0}", Regs.IsSet(Flags.CARRY) ? "C" : "-"));
            return sb.ToString();
        }
    }
}
