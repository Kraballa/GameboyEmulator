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
    }
}
