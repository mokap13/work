using IntBUSAdapter.IntbusInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntbusAdapterTest
{
    [TestClass]
    public class InbusInterfaceTest
    {
        [TestMethod]
        public void InterfaceNumberTest()
        {
            IntbusInterface intbusInterface = new UART0();
            Assert.AreEqual(1, intbusInterface.Number);
        }
    }
}
