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
        public FetchMode FetchMode = FetchMode.ZERO;
        public bool ReportOpcodes = false;

        private Queue<byte> TestData = new Queue<byte>();
        private Random Random = new Random();

        public TestCPU(Rom rom = null) : base(rom)
        {
            OCHandleMode = OCHandleMode.PRINT;
        }

        public void LoadTestData(params byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                TestData.Enqueue(data[i]);
            }
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
                    return 0;
                case FetchMode.RANDOM:
                    byte[] data = new byte[1];
                    Random.NextBytes(data);
                    return data[0];
            }
        }

        protected override int Execute(byte opcode)
        {
            if (ReportOpcodes)
                Console.WriteLine("opcode: {0:X}", opcode);
            return base.Execute(opcode);
        }

        public void InjectOpcode(byte opcode)
        {
            Execute(opcode);
        }

    }
}
