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

        private const int DivThreshold = 16384;

        private Memory Memory { get; set; }
        // count up cycles
        private int CycleCounter { get; set; }
        // div 
        private int DivCounter { get; set; }
        // Increments per second, in Hz
        private int ClockFrequency { get; set; }

        private int ClockThreshold(int hz) => (Controller.Instance.TargetFPS * CPU.CyclesPerFrame) / hz;

        public Timer()
        {
            Memory = CPU.Instance.Memory;
            SetClockFreq();
        }

        public void Update(int cycles)
        {
            if ((Memory[TAC] | (byte)TACReg.TIMERSTOP) > 0)
            {
                CycleCounter += cycles;
                DivCounter += cycles;
                if (CycleCounter > ClockThreshold(ClockFrequency))
                {
                    CycleCounter -= ClockThreshold(ClockFrequency);
                    IncrementTIMA();
                }

                if (DivCounter > ClockThreshold(DivThreshold))
                {
                    DivCounter -= ClockThreshold(DivThreshold);
                    IncrementDIV();
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

        private void IncrementTIMA()
        {
            Memory[TIMA]++;
            if (Memory[TIMA] == 0)
            {
                Memory[TIMA] = Memory[TMA];
                CPU.Instance.RequestInterrupt(InterruptType.TIMER);
            }
        }

        private void IncrementDIV()
        {
            Memory[DIV]++;
        }
    }
}
