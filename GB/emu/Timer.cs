using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum TACReg
    {
        TIMERSTOP = 0b100,
        CLOCKSEL = 0b11
    }

    public class Timer
    {
        public const ushort DIV = 0xFF04;
        public const ushort TIMA = 0xFF05;
        public const ushort TMA = 0xFF06;
        public const ushort TAC = 0xFF07;

        private Memory Memory { get; set; }

        private int TimerCounter { get; set; }

        public Timer(Memory mem)
        {
            Memory = mem;
        }

        public void Update(int cycles)
        {
            UpdateDividerRegister(cycles);
            if ((Memory[TAC] & (byte)TACReg.TIMERSTOP) > 0)
            {
                TimerCounter -= cycles;
                if (TimerCounter <= 0)
                {
                    SetClockFreq();

                    if (Memory[TIMA] == 255)
                    {
                        Memory[TIMA] = Memory[TMA];
                        CPU.Instance.RequestInterrupt(InterruptType.TIMER);
                    }
                    else
                    {
                        Memory[TIMA]++;
                    }
                }
            }
        }

        public void SetClockFreq()
        {
            switch (Memory[TAC] & (byte)TACReg.CLOCKSEL)
            {
                case 0: TimerCounter = 1024; break;
                case 1: TimerCounter = 16; break;
                case 2: TimerCounter = 64; break;
                case 3: TimerCounter = 256; break;
            }
        }

        private void UpdateDividerRegister(int cycles)
        {
            Memory.Mem[DIV] += (byte)cycles;
        }
    }
}
