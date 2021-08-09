using GB.emu;
using System;

namespace GBTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("emulator testing");

            TestCPU CPU = new TestCPU(new Rom("cpu_instrs.gb"), Flags.ZERO);
            CPU.ReportOpcodes = true;
            CPU.Run();
        }
    }
}
