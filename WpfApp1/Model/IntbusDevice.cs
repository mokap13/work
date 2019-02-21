using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WpfApp1.Helpers;

namespace WpfApp1.Model
{
    [JsonObject("intbusDevice")]
    public class IntbusDevice
    {
        static Dictionary<InterfaceName, int> interfaceCodeDictionary = new Dictionary<InterfaceName, int>
        {
            {InterfaceName.UART, 0b001 },
            {InterfaceName.FM,   0b010 },
            {InterfaceName.SPI,  0b011 },
            {InterfaceName.I2C,  0b100 },
            {InterfaceName.OWI,  0b101 }
        };

        private IntbusDevice parentDevice;

        private byte PreambuleByte => (byte)(this.IntbusAddress | (interfaceCodeDictionary[this.Interface] << 5));

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("interface")]
        [JsonRequired]
        public InterfaceName Interface { get; private set; }

        [JsonRequired]
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("intbusAddress")]
        [JsonRequired]
        public int IntbusAddress { get; private set; }

        [JsonProperty("modbusAddress")]
        [JsonRequired]
        public int ModbusAddress { get; private set; }

        [JsonProperty("virtualModbusAddress")]
        public int? VirtualModbusAddress { get; private set; }

        [JsonProperty("intbusDevices")]
        public List<IntbusDevice> Devices { get; private set; }

        public void InitializeParents()
        {
            if(this.Devices != null)
            {
                this.Devices.ForEach(d =>
                {
                    d.parentDevice = this;
                    d.InitializeParents();
                });
            }
        }

        public void InitializeAddress(ref Dictionary<int, IntbusDevice> addressDeviceDictionary)
        {
            if(this.VirtualModbusAddress != null)
            {
                if (addressDeviceDictionary.ContainsKey((int)this.VirtualModbusAddress))
                    throw new Exception($"{this.Name} and " +
                        $"{addressDeviceDictionary[(int)this.VirtualModbusAddress].Name}: " +
                        $"equal modbus address :{this.VirtualModbusAddress} ");
                addressDeviceDictionary.Add((int)this.VirtualModbusAddress, this);
            }

            if (this.Devices != null)
                foreach (IntbusDevice device in this.Devices)
                    device.InitializeAddress(ref addressDeviceDictionary);
        }

        public List<byte> CalculatePreambule()
        {
            IntbusDevice device = this;
            List<byte> bytes = new List<byte>();
            while (device != null)
            {
                bytes.Add(device.PreambuleByte);
                device = device.parentDevice;
            }
            bytes.Reverse();
            return bytes;
        }

        public List<byte> ConvertToIntbus(List<byte> modbusFrame)
        {
            if (modbusFrame.First() != this.VirtualModbusAddress)
                throw new ArgumentException($"{this.Name} mbAddr:{this.VirtualModbusAddress} адрес устройства не соответствует адресу в словаре");

            List<byte> preambule = this.CalculatePreambule();
            modbusFrame[0] = (byte)this.ModbusAddress;
            modbusFrame.InsertRange(0, preambule);
            modbusFrame.RemoveRange(modbusFrame.Count - 2, 2);
            byte[] crc = ModbusUtility.CalculateCrc(modbusFrame.ToArray());
            modbusFrame.AddRange(crc.ToList());

            return modbusFrame;
        }

        public enum InterfaceName
        {
            UART,
            FM,
            I2C,
            SPI,
            OWI
        }
    }
}
