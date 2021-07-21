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
            cpu.ReportOpcodes = true;
            for (byte opcode = 0x00; opcode <= 0x7F; opcode++)
            {
                cpu.LoadTestData(opcode, 0x10).Run().ClearTestData();
            }
        }
    }
}
