using GB.emu;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace GBTesting
{
    [TestClass]
    public class BasicTests
    {

        [TestMethod]
        public void TestEmptyRom()
        {
            TestCPU cpu = new TestCPU(Rom.Empty);
        }

        [TestMethod]
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

            Assert.IsTrue(cpu.IsSet(Flags.ZERO | Flags.SUB));
            cpu.LoadTestData(0x15, 0x10).Run();
            Assert.AreEqual(0xFE, cpu.Regs.D);
            Assert.IsTrue(cpu.IsSet(Flags.SUB));
        }
    }
}
