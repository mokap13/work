using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfApp1.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;

namespace WpfApp1.Model.Tests
{
    [TestClass()]
    public class IntbusDeviceTests
    {
        private Dictionary<int, IntbusDevice> modbusAddressDictionary = new Dictionary<int, IntbusDevice>();
        private List<IntbusDevice> intbusDevices;

        public IntbusDeviceTests()
        {
            List<IntbusDevice> devices = CreateDevices();
        }
        List<IntbusDevice> CreateDevices()
        {
            JObject jObj = JObject.Parse(File.ReadAllText(@"./intbus_device.json"));
            intbusDevices = jObj["intbusDevice"].ToObject<List<IntbusDevice>>();
            intbusDevices.ForEach(d => d.InitializeParents());

            foreach (IntbusDevice device in intbusDevices)
                device.InitializeAddress(ref modbusAddressDictionary);

            return intbusDevices;
        }

        [TestMethod()]
        public void CalculateFrameTest()
        {
            //01 03 00 00 00 01 84 0A
            //41 21 01 03 00 00 00 01 70 EF

            List<byte> mbFrame = new List<byte> { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A };
            List<byte> expected = new List<byte> { 0x41, 0x21, 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x30, 0xE4 };

            List<byte> actual = modbusAddressDictionary[mbFrame.First()].ConvertToIntbus(mbFrame);

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod()]
        public void CalculateFrameTest2()
        {
            List<byte> mbFrame = new List<byte>
            {
                0x02,0x04,0x02,0x12,0x34,
                0xB4, 0x47
            };
            List<byte> expected = new List<byte>
            {
                0x21,0xA1,
                0x01,0x04,0x02,0x12,0x34,
                0xA3, 0x34
            };

            List<byte> actual = modbusAddressDictionary[mbFrame.First()].ConvertToIntbus(mbFrame);

            CollectionAssert.AreEqual(expected, actual);
        }
        
    }
}