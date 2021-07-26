using GB.emu;
using System;

namespace GBTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("emulator testing");

            TestCPU CPU = new TestCPU(new Rom("tetris.gb"));
            CPU.ReportOpcodes = true;
            CPU.Regs.Set(Flags.CARRY | Flags.HCARRY | Flags.SUB | Flags.ZERO);
            CPU.Regs.Unset(Flags.CARRY | Flags.HCARRY | Flags.SUB | Flags.ZERO);
            Console.WriteLine(CPU.FlagsToString());
            CPU.Step();


        }

        private static void PrintUnknownOpcodes()
        {
            TestCPU cpu = new TestCPU(Rom.Empty);
            cpu.OCErrorMode = OCErrorMode.PRINT;
            cpu.FetchMode = FetchMode.ZERO;
            cpu.ReportOpcodes = false;
            Console.WriteLine("missing 8 bit opcodes:");
            for (int opcode = 0x00; opcode <= 0xFF; opcode++)
            {
                cpu.LoadTestData((byte)opcode).Run();
            }

            Console.WriteLine("missing 16 bit opcodes:");
            for (int opcode = 0x00; opcode <= 0xFF; opcode++)
            {
                cpu.LoadTestData(0xCB, (byte)opcode).Run();
            }
        }
    }
}
