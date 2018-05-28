using System;
using System.Collections.Generic;
using IntBUSAdapter;
using IntBUSAdapter.IntbusInterfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IntbusAdapterTest
{
    [TestClass]
    public class IntbusDeviceTest
    {
        [TestMethod]
        public void AddressWithInterfaceTest()
        {
            IntbusDevice intbusDevice = new IntbusDevice(new UART0(), 5);
            int expected = 37;
            int actual = intbusDevice.AddressWithInterface;
            Assert.AreEqual(expected, actual);
        }
        [TestMethod]
        public void ConvertToIntbusFrameTest()
        {
            IntbusDevice intbusDevice = new IntbusDevice(new UART0(), address: 1);
            IntbusDevice intbusDeviceSlave = new IntbusDevice(new OWI(), address: 1);
            intbusDevice.AddIntbusDevice(intbusDeviceSlave);

            List<byte> modbusFrame = new List<byte>
            {
                0x01,0x04,0x02,0x12,0x34,
                0xB4, 0x47
            };
            List<byte> expected = new List<byte>
            {
                0x21,0xA1,
                0x01,0x04,0x02,0x12,0x34,
                0xA3, 0x34
            };
            List<byte> actual = intbusDeviceSlave.ConvertToIntbusFrame(modbusFrame);
            CollectionAssert.AreEqual(expected, actual);
        }
        [TestMethod]
        public void ConvertToIntbusFrameWithPrePostfixBytesTest()
        {
            IntbusDevice intbusDevice = new IntbusDevice(new SPI(), address: 1);
            intbusDevice.PrefixBytes.Add(0xFF);
            intbusDevice.PostfixBytes.Add(0xFF);
            List<byte> modbusFrame = new List<byte>
            {
                0x01, 0x04, 0x00, 0x05, 0x00, 0x01,
                0x00, 0x00
            };
            List<byte> expected = new List<byte>
            {
                0xFF,
                0x61, 
                0x01, 0x04, 0x00, 0x05, 0x00, 0x01,
                0x4A, 0x16,
                0xFF
            };
            List<byte> actual = intbusDevice.ConvertToIntbusFrame(modbusFrame);
            CollectionAssert.AreEqual(expected, actual);
        }

    }
}
