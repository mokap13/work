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
            CreateDevices();
        }
        private void CreateDevices()
        {
            JObject jObj = JObject.Parse(File.ReadAllText(@"../../intbus_device.json"));
            intbusDevices = jObj["intbusDevices"].ToObject<List<IntbusDevice>>();
            intbusDevices.ForEach(d => d.InitializeParents());

            foreach (IntbusDevice device in intbusDevices)
                device.InitializeAddress(ref modbusAddressDictionary);
        }

        [TestMethod()]
        public void CalculateFrameTest()
        {
            //01 03 00 00 00 01 84 0A
            //41 21 01 03 00 00 00 01 70 EF

            List<byte> mbFrame = new List<byte> { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A };
            List<byte> expected = new List<byte> { 0x41, 0x21, 0x07, 0x03, 0x00, 0x00, 0x00, 0x01, 0x30, 0x82 };

            List<byte> actual = modbusAddressDictionary[mbFrame.First()].ConvertToIntbus(mbFrame);

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod()]
        public void CalculateFrameTest3()
        {
            List<byte> mbFrame = new List<byte>
            {
                0x04, 0x04, 0x00, 0x05, 0x00, 0x01, 0x21, 0x9E,
            };
            List<byte> expected = new List<byte>
            {
                0x21, 0xA1, 0x09, 0x04, 0x00, 0x05, 0x00, 0x01, 0x13, 0x8D,
            };

            List<byte> actual = modbusAddressDictionary[mbFrame.First()].ConvertToIntbus(mbFrame);

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod()]
        public void CalculateFrameTest4()
        {
            List<byte> mbFrame = new List<byte>
            {
                0x03, 0x04, 0x00, 0x05, 0x00, 0x01, 0x21, 0x9E,
            };
            List<byte> expected = new List<byte>
            {
                0x21, 0x08, 0x04, 0x00, 0x05, 0x00, 0x01, 0x0B, 0x4B,
            };

            List<byte> actual = modbusAddressDictionary[mbFrame.First()].ConvertToIntbus(mbFrame);

            CollectionAssert.AreEqual(expected, actual);
        }

    }
}