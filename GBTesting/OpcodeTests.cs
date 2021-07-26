using GB.emu;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GBTesting
{
    public class OpcodeTests
    {
        private TestCPU CPU;

        public OpcodeTests()
        {
            Setup();
        }

        public void Setup()
        {
            CPU = new TestCPU(Rom.Empty);
            CPU.FetchMode = FetchMode.ZERO;
            CPU.OCErrorMode = OCErrorMode.ERROR;
        }

        public void Cleanup()
        {
            CPU = null;
        }

        public void Test8BitLDDEC()
        {
            TestCPU cpu = new TestCPU(Rom.Empty);
            cpu.ReportOpcodes = true;
            cpu.LoadTestData(
                0x06, 0x01, //LD B, 0x01
                0x16, 0xFF, //LD D, 0xFF
                0x26, 0xFF, //LD H, 0xFF
                0x05,       //DEC B
                0x10        //STOP
                ).Run();
            Assert.AreEqual(0x00, cpu.Regs.B);
            Assert.AreEqual(0xFF, cpu.Regs.D);
            Assert.AreEqual(0xFF, cpu.Regs.H);
            cpu.ClearTestData();
            Assert.IsTrue(cpu.Regs.IsSet(Flags.ZERO | Flags.SUB));
            cpu.LoadTestData(0x15, 0x10).Run();
            Assert.AreEqual(0xFE, cpu.Regs.D);
            Assert.IsTrue(cpu.Regs.IsSet(Flags.SUB));
        }

        public void TestRL()
        {
            TestCPU cpu = new TestCPU(Rom.Empty);
            cpu.ReportOpcodes = true;
            cpu.LoadTestData(
                0x06, 0x01, //LD B, 1
                0xCB, 0x00 //RLC B
                ).Run();
            Assert.AreEqual(0b00000010, cpu.Regs.B);
            Assert.IsTrue(!cpu.Regs.IsSet(Flags.ZERO | Flags.SUB | Flags.CARRY | Flags.HCARRY));
            cpu.LoadTestData(
                0x06, 0b11111110, //LD B, 1
                0xCB, 0x00 //RLC B
                ).Run();
            Assert.AreEqual(0b11111101, cpu.Regs.B);
            Assert.IsTrue(!cpu.Regs.IsSet(Flags.ZERO | Flags.SUB | Flags.HCARRY) && cpu.Regs.IsSet(Flags.CARRY));
        }
    }
}
