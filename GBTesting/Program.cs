using System;

namespace GBTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("emulator testing");
            TestCPU cpu = new TestCPU();
            cpu.FetchMode = FetchMode.RANDOM;
            cpu.ReportOpcodes = true;

            cpu.PrintDebugInfo();

            cpu.InjectOpcode(0x01);
            cpu.InjectOpcode(0x03);
            cpu.InjectOpcode(0x05);

            cpu.PrintDebugInfo();
        }
    }
}
