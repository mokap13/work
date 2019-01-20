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

        private byte PreambuleByte => (byte)(this.Address | (interfaceCodeDictionary[this.Interface] << 5));

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("interface")]
        [JsonRequired]
        public InterfaceName Interface { get; private set; }

        [JsonRequired]
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        [JsonRequired]
        public int Address { get; private set; }

        [JsonProperty("modbusAddress")]
        public int? ModbusAddress { get; private set; }

        [JsonProperty("intbusDevice")]
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
            if(this.ModbusAddress != null)
            {
                if (addressDeviceDictionary.ContainsKey((int)this.ModbusAddress))
                    throw new Exception($"{this.Name} and " +
                        $"{addressDeviceDictionary[(int)this.ModbusAddress].Name}: " +
                        $"equal modbus address :{this.ModbusAddress} ");
                addressDeviceDictionary.Add((int)this.ModbusAddress, this);
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
            if (modbusFrame.First() != this.ModbusAddress)
                throw new ArgumentException($"{this.Name} mbAddr:{this.ModbusAddress} адрес устройства не соответствует адресу в словаре");

            List<byte> preambule = this.CalculatePreambule();

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
