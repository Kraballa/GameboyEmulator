using GB.emu;
using System;

namespace GBTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("emulator testing");

            PrintUnknownOpcodes();

            //new BasicTests().Test8BitLDDEC();
        }

        private static void PrintUnknownOpcodes()
        {
            TestCPU cpu = new TestCPU(Rom.Empty);
            cpu.OCErrorMode = OCErrorMode.PRINT;
            cpu.FetchMode = FetchMode.ZERO;
            cpu.ReportOpcodes = false;
            for (int opcode = 0x00; opcode <= 0xFF; opcode++)
            {
                cpu.LoadTestData((byte)opcode).Run();
            }
        }
    }
}
