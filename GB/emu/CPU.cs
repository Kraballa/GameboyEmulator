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

        public Flags Flags = 0;
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
            Regs.A = 0x01;
            Regs.BC = 0x0013;
            Regs.DE = 0x00D8;
            Regs.HL = 0x014D;
            Regs.SP = 0xFFFE;
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
                    Memory.IMEF = true;
                    Regs.PC = Memory.Pop();
                    Cycles = 4;
                    break;
                case 0xCB:
                    Cycles = Execute16Bit(Fetch());
                    break;
                case 0x2F:
                    Regs.A ^= 0b11111111; //flip all bits
                    Set(Flags.SUB | Flags.HCARRY);
                    Cycles = 1;
                    break;
                case 0x3F:
                    Unset(Flags.SUB | Flags.HCARRY);
                    Flags ^= Flags.CARRY;
                    break;
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
                #endregion

                #region JR NX, s8
                case 0x20:
                    Cycles = 2;
                    if (!IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)(Regs.PC + Fetch());
                        Cycles++;
                    }
                    break;
                case 0x30:
                    Cycles = 2;
                    if (!IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)(Regs.PC + Fetch());
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

                #region JR [0|Z|C], s8
                case 0x18:
                    Regs.PC = (ushort)(Regs.PC + Fetch());
                    Cycles = 3;
                    break;
                case 0x28:
                    Cycles = 2;
                    if (IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)(Regs.PC + Fetch());
                        Cycles++;
                    }
                    break;
                case 0x38:
                    Cycles = 2;
                    if (IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)(Regs.PC + Fetch());
                        Cycles++;
                    }
                    break;
                #endregion

                #region ADD HL, XY
                case 0x09:
                case 0x19:
                case 0x29:
                case 0x39:
                    Place((uint)Regs.HL + Regs[HighBit + 1] > ushort.MaxValue, Flags.CARRY | Flags.HCARRY);
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
                    Regs.SetHigh(HighBit - 3, Regs.GetByteByIndex(LowBit + 1));
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
                    Memory[Regs.HL] = Regs.GetByteByIndex(LowBit + 1);
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
                    Regs.SetLow(HighBit - 3, Regs.GetByteByIndex(LowBit + 1));
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
                    Regs.A = Regs.GetByteByIndex(LowBit + 1);
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
                        Unset(Flags.SUB);
                        int val = Regs.GetByteByIndex(LowBit + 1);
                        Place(Regs.A + val > ushort.MaxValue, Flags.CARRY | Flags.HCARRY);
                        Regs.A += (byte)val;
                        Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;

                case 0x86:
                    {
                        int val = Regs.A + Memory[Regs.HL];
                        Regs.A += Memory[Regs.HL];
                        Place(val > byte.MaxValue, Flags.CARRY | Flags.HCARRY);
                        Unset(Flags.SUB);
                        Place(Regs.A == 0, Flags.ZERO);
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
                        Set(Flags.SUB);
                        int val = Regs.GetByteByIndex(LowBit + 1);
                        Place(Regs.A - val < 0, Flags.CARRY | Flags.HCARRY);
                        Regs.A += (byte)val;
                        Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;
                case 0x96:
                    {
                        int val = Regs.A - Memory[Regs.HL];
                        Regs.A -= Memory[Regs.HL];
                        Place(val < 0, Flags.CARRY | Flags.HCARRY);
                        Set(Flags.SUB);
                        Place(Regs.A == 0, Flags.ZERO);
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
                    Regs.A &= Regs.GetByteByIndex(LowBit + 1);
                    Set(Flags.HCARRY);
                    Unset(Flags.SUB | Flags.CARRY);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xA6:
                    Regs.A &= Memory[Regs.HL];
                    Set(Flags.HCARRY);
                    Unset(Flags.SUB | Flags.CARRY);
                    Place(Regs.A == 0, Flags.ZERO);
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
                    Regs.A |= Regs.GetByteByIndex(LowBit + 1);
                    Unset(Flags.SUB | Flags.HCARRY | Flags.CARRY);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xB6:
                    Regs.A |= Memory[Regs.HL];
                    Unset(Flags.SUB | Flags.HCARRY | Flags.CARRY);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 2;
                    break;
                #endregion

                #region things with A
                case 0x87:
                    Place(Regs.A > 0x0F, Flags.CARRY | Flags.HCARRY);
                    Regs.A += Regs.A;
                    Unset(Flags.SUB);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x97:
                    Regs.A = 0;
                    Set(Flags.SUB | Flags.ZERO);
                    Unset(Flags.CARRY | Flags.HCARRY);
                    Cycles = 1;
                    break;
                case 0xA7:
                    FlushFlags(Flags.HCARRY);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xB7:
                    FlushFlags(0);
                    Place(Regs.A == 0, Flags.ZERO);
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
                        Unset(Flags.SUB);
                        int val = Regs.GetByteByIndex(LowBit + 1);
                        val += IsSet(Flags.CARRY) ? 1 : 0;
                        Place(Regs.A + val > ushort.MaxValue, Flags.CARRY | Flags.HCARRY);
                        Regs.A += (byte)val;
                        Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;

                case 0x8E:
                    {
                        int val = Regs.A + Memory[Regs.HL];
                        val += IsSet(Flags.CARRY) ? 1 : 0;
                        Regs.A += Memory[Regs.HL];
                        Place(val > byte.MaxValue, Flags.CARRY | Flags.HCARRY);
                        Unset(Flags.SUB);
                        Place(Regs.A == 0, Flags.ZERO);
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
                        Set(Flags.SUB);
                        byte val = Regs.GetByteByIndex(LowBit + 1);
                        Place(Regs.A - val < 0, Flags.CARRY | Flags.HCARRY);
                        Regs.A += val;
                        Place(Regs.A == 0, Flags.ZERO);
                        Cycles = 1;
                    }
                    break;
                case 0x9E:
                    {
                        int val = Regs.A - Memory[Regs.HL];
                        Regs.A -= Memory[Regs.HL];
                        Place(val < 0, Flags.CARRY | Flags.HCARRY);
                        Set(Flags.SUB);
                        Place(Regs.A == 0, Flags.ZERO);
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
                    Regs.A ^= Regs.GetByteByIndex(LowBit + 1);
                    Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xAE:
                    Regs.A ^= Memory[Regs.HL];
                    Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Place(Regs.A == 0, Flags.ZERO);
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
                    Place(Regs.A == Regs.B, Flags.ZERO);
                    Set(Flags.SUB);
                    Place(Regs.A < Regs.B, Flags.CARRY);
                    Cycles = 1;
                    break;
                case 0xBE:
                    Place(Regs.A == Memory[Regs.HL], Flags.ZERO);
                    Place(Regs.A < Memory[Regs.HL], Flags.CARRY);
                    Cycles = 2;
                    break;
                #endregion

                #region More A shenanigans
                case 0x8F:
                    Place(Regs.A * 2 + (IsSet(Flags.CARRY) ? 1 : 0) > byte.MaxValue, Flags.CARRY | Flags.HCARRY);
                    Unset(Flags.SUB);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0x9F:
                    Regs.A = (byte)(IsSet(Flags.CARRY) ? 1 : 0);
                    Set(Flags.SUB);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xAF:
                    Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
                    Place(Regs.A == 0, Flags.ZERO);
                    Cycles = 1;
                    break;
                case 0xBF:
                    Set(Flags.ZERO | Flags.SUB);
                    Unset(Flags.HCARRY | Flags.CARRY);
                    Cycles = 1;
                    break;
                #endregion

                case 0xC0:
                    Cycles = 2;
                    if (!IsSet(Flags.ZERO))
                    {
                        Regs.PC = Memory.Pop();
                        Cycles += 3;
                    }
                    break;

                case 0xD0:
                    Cycles = 2;
                    if (!IsSet(Flags.CARRY))
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
                    if (!IsSet(Flags.ZERO))
                    {
                        Regs.PC = (ushort)(Fetch() | (Fetch() << 8));
                        Cycles = 4; ;
                    }
                    else
                    {
                        Cycles = 3;
                    }
                    break;
                case 0xD2:
                    if (!IsSet(Flags.CARRY))
                    {
                        Regs.PC = (ushort)(Fetch() | (Fetch() << 8));
                        Cycles = 4; ;
                    }
                    else
                    {
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
                    Regs.PC = (ushort)(Fetch() | (Fetch() << 8));
                    Cycles = 4;
                    break;

                case 0xC4:
                    if (!IsSet(Flags.ZERO))
                    {
                        Memory.Push(Regs.PC);
                        Regs.PC = (ushort)(Fetch() | (Fetch() << 8));
                        Cycles = 6;
                    }
                    else
                    {
                        Cycles = 3;
                    }
                    break;
                case 0xD4:
                    if (!IsSet(Flags.CARRY))
                    {
                        Memory.Push(Regs.PC);
                        Regs.PC = (ushort)(Fetch() | (Fetch() << 8));
                        Cycles = 6;
                    }
                    else
                    {
                        Cycles = 3;
                    }
                    break;

                case 0xC5:
                case 0xD5:
                case 0xE5:
                    Memory.Push(Regs[HighBit - 0x8]);
                    Cycles = 4;
                    break;
                case 0xF5:
                    Memory.Push(Regs.AF);
                    Cycles = 4;
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
            Memory.IMEF = false;
            Memory[Memory.IFREG] &= (byte)~(1 << (int)type);
            Memory.Push(Regs.PC);

            switch (type)
            {
                case InterruptType.VBlank:
                    Regs.PC = 0x40;
                    break;
                case InterruptType.LCD:
                    Regs.PC = 0x48;
                    break;
                case InterruptType.Timer:
                    Regs.PC = 0x50;
                    break;
                case InterruptType.Joypad:
                    Regs.PC = 0x60;
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
