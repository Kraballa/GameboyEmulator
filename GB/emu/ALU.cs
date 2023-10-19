using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    /// <summary>
    /// Arithmetic Logic Unit. 
    /// 
    /// heavily inspired by https://github.com/trekawek/coffee-gb/blob/master/src/main/java/eu/rekawek/coffeegb/cpu/AluFunctions.java
    /// </summary>
    public class ALU
    {
        private Registers Regs { get; set; }

        public ALU()
        {
            Regs = CPU.Instance.Regs;
        }

        public byte INCD8(int arg)
        {
            int result = (arg + 1) & 0xFF;
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB);
            Regs.Place((arg & 0x0F) == 0x0F, Flags.HCARRY);
            return (byte)result;
        }

        public byte INCD16(int arg)
        {
            return (byte)((arg + 1) & 0xFFFF);
        }

        public byte DECD8(int arg)
        {
            int result = (arg - 1) & 0xFF;
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Set(Flags.SUB);
            Regs.Place((arg & 0x0F) == 0x00, Flags.HCARRY);
            return (byte)result;
        }

        public byte DECD16(int arg)
        {
            return (byte)((arg - 1) & 0xFFFF);
        }

        public byte ADD16(int arg1, int arg2)
        {
            Regs.Unset(Flags.SUB);
            Regs.Place((arg1 & 0x0FFF) + (arg2 & 0x0FFF) > 0x0FFF, Flags.HCARRY);
            Regs.Place((arg1 + arg2) > 0xFFFF, Flags.CARRY);
            return (byte)((arg1 + arg2) & 0xFFFF);
        }

        public byte ADDD16D8(int arg1, int arg2)
        {
            return (byte)((arg1 + arg2) & 0xFFFF);
        }

        public byte ADDSP(int arg1, int arg2)
        {
            Regs.Unset(Flags.ZERO);
            Regs.Unset(Flags.SUB);

            int result = arg1 + arg2;
            Regs.Place((((arg1 & 0xff) + (arg2 & 0xff)) & 0x100) != 0, Flags.CARRY);
            Regs.Place((((arg1 & 0x0f) + (arg2 & 0x0f)) & 0x10) != 0, Flags.HCARRY);
            return (byte)result;
        }

        public byte DAA(int arg)
        {
            int result = arg;
            if (Regs.IsSet(Flags.SUB))
            {
                if (Regs.IsSet(Flags.HCARRY))
                {
                    result = (result - 6) & 0xFF;
                }

                if (Regs.IsSet(Flags.CARRY))
                {
                    result = (result - 0x60) & 0xFF;
                }
            }
            else
            {
                if (Regs.IsSet(Flags.HCARRY) || (result & 0xF) > 9)
                {
                    result += 0x6;
                }

                if (Regs.IsSet(Flags.CARRY) || result > 0x9F)
                {
                    result += 0x60;
                }
            }
            Regs.Unset(Flags.SUB);
            if (result > 0xFF)
            {
                Regs.Set(Flags.CARRY);
            }
            result &= 0xFF;
            Regs.Place(result == 0, Flags.ZERO);
            return (byte)result;
        }

        public byte CPL(int arg)
        {
            Regs.Set(Flags.SUB | Flags.HCARRY);
            return (byte)((~arg) & 0xFF);
        }

        public byte SCF(int arg)
        {
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            Regs.Set(Flags.CARRY);
            return (byte)arg;
        }

        public byte CCF(int arg)
        {
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            Regs.Flip(Flags.CARRY);
            return (byte)arg;
        }

        public byte ADDD8D8(int arg1, int arg2)
        {
            int value = (arg1 + arg2) & 0xFF;
            Regs.Place(value == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB);
            Regs.Place((arg1 & 0xF) + (arg2 & 0xF) > 0xF, Flags.HCARRY);
            Regs.Place(arg1 + arg2 > 0xFF, Flags.CARRY);
            return (byte)value;
        }

        public byte ADC(int arg1, int arg2)
        {
            int carry = Regs.IsSet(Flags.CARRY) ? 1 : 0;
            Regs.Place(((arg1 + arg2 + carry) & 0xff) == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB);
            Regs.Place((arg1 & 0x0f) + (arg2 & 0x0f) + carry > 0x0f, Flags.HCARRY);
            Regs.Place(arg1 + arg2 + carry > 0xff, Flags.CARRY);
            return (byte)((arg1 + arg2 + carry) & 0xff);
        }

        public byte SUBD8D8(int arg1, int arg2)
        {
            int value = (arg1 - arg2) & 0xFF;
            Regs.Place(value == 0, Flags.ZERO);
            Regs.Set(Flags.SUB);
            Regs.Place((arg2 & 0xF) > (arg1 & 0xF), Flags.HCARRY);
            Regs.Place(arg2 > arg1, Flags.CARRY);
            return (byte)value;
        }

        public byte SBC(int arg1, int arg2)
        {
            int carry = Regs.IsSet(Flags.CARRY) ? 1 : 0;
            int result = arg1 - arg2 - carry;

            Regs.Place((result & 0xff) == 0, Flags.ZERO);
            Regs.Set(Flags.SUB);
            Regs.Place(((arg1 ^ arg2 ^ (result & 0xff)) & (1 << 4)) != 0, Flags.HCARRY);
            Regs.Place(result < 0, Flags.CARRY);
            return (byte)(result & 0xFF);
        }

        public byte AND(int arg1, int arg2)
        {
            int result = arg1 & arg2;
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.CARRY);
            Regs.Set(Flags.HCARRY);
            return (byte)result;
        }

        public byte OR(int arg1, int arg2)
        {
            int result = arg1 | arg2;
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
            return (byte)result;
        }

        public byte XOR(int arg1, int arg2)
        {
            int result = (arg1 ^ arg2) & 0xFF;
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
            return (byte)result;
        }

        public byte CP(int arg1, int arg2)
        {
            Regs.Place(((arg1 - arg2) & 0xff) == 0, Flags.ZERO);
            Regs.Set(Flags.SUB);
            Regs.Place((0xF & arg1) > (0xF & arg2), Flags.HCARRY);
            Regs.Place(arg2 > arg1, Flags.CARRY);
            return (byte)arg1;
        }

        public byte RLC(int arg)
        {
            int result = (arg << 1) & 0xFF;
            if ((arg & (1 << 7)) != 0)
            {
                result |= 1;
                Regs.Set(Flags.CARRY);
            }
            else
            {
                Regs.Unset(Flags.CARRY);
            }
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte RRC(int arg)
        {
            int result = (arg >> 1);
            if ((arg & 1) == 1)
            {
                result |= (1 << 7);
                Regs.Set(Flags.CARRY);
            }
            else
            {
                Regs.Unset(Flags.CARRY);
            }
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte RL(int arg)
        {
            int result = (arg << 1) & 0xFF;
            result |= Regs.IsSet(Flags.CARRY) ? 1 : 0;
            Regs.Place((arg & (1 << 7)) != 0, Flags.CARRY);
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte RR(int arg)
        {
            int result = arg >> 1;
            result |= Regs.IsSet(Flags.CARRY) ? (1 << 7) : 0;
            Regs.Place((arg & 1) != 0, Flags.CARRY);
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte SLA(int arg)
        {
            int result = (arg << 1) & 0xFF;
            Regs.Place((arg & (1 << 7)) != 0, Flags.CARRY);
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte SRA(int arg)
        {
            int result = (arg >> 1) | (arg & (1 << 7)); //TODO: doublecheck
            Regs.Place((arg & 1) != 0, Flags.CARRY);
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte SWAP(int arg)
        {
            int upper = arg & 0xF0;
            int lower = arg & 0x0F;
            int result = (lower << 4) | (upper >> 4);
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.CARRY | Flags.HCARRY);
            return (byte)result;
        }

        public byte SRL(int arg)
        {
            int result = (arg >> 1);
            Regs.Place((arg & 1) != 0, Flags.CARRY);
            Regs.Place(result == 0, Flags.ZERO);
            Regs.Unset(Flags.SUB | Flags.HCARRY);
            return (byte)result;
        }

        public byte BIT(int arg1, int arg2)
        {
            Regs.Unset(Flags.SUB);
            Regs.Set(Flags.HCARRY);
            Regs.Place((arg1 & (1 << arg2)) == 0, Flags.ZERO);
            return (byte)arg1;
        }

        public byte RES(int arg1, int arg2)
        {
            return (byte)((arg1 & ~(1 << arg2)) & 0xFF);
        }

        public byte SET(int arg1, int arg2)
        {
            return (byte)((arg1 | (1 << arg2)) & 0xFF);
        }
    }
}
