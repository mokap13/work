using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter
{
    public class ModbusAddressesManager
    {
        public Dictionary<int, IntbusDevice> ModbusDeviceAddresses { get; set; }

        public ModbusAddressesManager()
        {
            ModbusDeviceAddresses = new Dictionary<int, IntbusDevice>();
        }
    }
}
