using GB.emu;
using System;
using System.Collections.Generic;
using System.Text;

namespace GBTesting
{
    public enum FetchMode
    {
        ROM,
        ZERO,
        RANDOM
    }

    /// <summary>
    /// 
    /// </summary>
    public class TestCPU : CPU
    {
        public FetchMode FetchMode = FetchMode.ROM;
        public bool ReportOpcodes = false;

        private Queue<byte> TestData = new Queue<byte>();
        private Random Random = new Random();

        public TestCPU(Rom rom, Flags flags) : this(rom)
        {
            Regs.FlushFlags(flags);
        }

        public TestCPU(Rom rom) : base(rom)
        {
            OCErrorMode = OCErrorMode.PRINT;

        }

        /// <summary>
        /// Inject code to be evaluated before further polling from the rom
        /// </summary>
        public TestCPU LoadTestData(params byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                TestData.Enqueue(data[i]);
            }
            return this;
        }

        public TestCPU ClearTestData()
        {
            TestData.Clear();
            return this;
        }

        /// <summary>
        /// Run opcodes until either STOP or HALT is called
        /// </summary>
        public TestCPU Run()
        {
            CPUMode = CPUMode.NORMAL;
            bool waitForPress = false;
            ReportOpcodes = false;
            while (CPUMode == CPUMode.NORMAL)
            {
                if (Regs.PC == 0x29A)
                {
                    ReportOpcodes = true;
                    waitForPress = true;
                }
                Execute(Fetch());
                if (waitForPress)
                {
                    Console.ReadKey();
                }

            }
            return this;
        }

        protected override byte Fetch()
        {
            if (TestData.Count > 0)
                return TestData.Dequeue();

            switch (FetchMode)
            {
                case FetchMode.ROM:
                    return base.Fetch();
                default:
                case FetchMode.ZERO:
                    return 0x10;
                case FetchMode.RANDOM:
                    byte[] data = new byte[1];
                    Random.NextBytes(data);
                    return data[0];
            }
        }

        protected override int Execute(byte opcode)
        {
            if (opcode == 0xCB) //16bit opcode
            {
                opcode = Fetch();
                if (ReportOpcodes)
                {
                    Console.WriteLine("opcode [16bit]: 0x{0:X}\t{1} - {2}", opcode | 0xCB00, FlagsToString(), Regs);
                }

                return base.Execute16Bit(opcode);
            }
            else
            {
                if (ReportOpcodes)
                {
                    Console.WriteLine("opcode  [8bit]: 0x{0:X}\t{1} - {2}", opcode, FlagsToString(), Regs);
                }

                return base.Execute(opcode);
            }
        }
    }
}
