using GB.emu.Memory;
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

        private MMU Memory { get; set; }
        // count up cycles
        private int TimerCounter { get; set; }
        // div 
        private int DivCounter { get; set; }
        // Increments per second, in Hz
        private int ClockFrequency { get; set; }

        public Timer()
        {
            Memory = CPU.Instance.Memory;
            SetClockFreq();
        }

        public void Update(int cycles)
        {
            DivCounter += cycles;
            if (DivCounter >= 256)
            {
                DivCounter -= 256;
                Memory[DIV]++;
            }

            if (((Memory[TAC] >> 2) & 0x1) > 0)
            {
                TimerCounter += cycles * 4;
                SetClockFreq();

                while (TimerCounter >= (CPU.CLOCKS_PER_SECOND / ClockFrequency))
                {
                    Memory[TIMA]++;
                    if (Memory[TIMA] == 0)
                    {
                        CPU.Instance.RequestInterrupt(InterruptType.TIMER);
                        Memory[TIMA] = Memory[TMA];
                    }
                    TimerCounter -= (CPU.CLOCKS_PER_SECOND / ClockFrequency);
                }
            }
        }

        public void SetClockFreq()
        {
            switch (Memory[TAC] & (byte)TACReg.CLOCKSEL)
            {
                case 0: ClockFrequency = 4096; break;
                case 1: ClockFrequency = 262144; break;
                case 2: ClockFrequency = 65536; break;
                case 3: ClockFrequency = 16384; break;
            }
        }
    }
}
