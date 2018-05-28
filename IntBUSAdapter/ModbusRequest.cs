using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter
{
    public class ModbusRequest
    {
        private int deviceAddress;

        public int DeviceAddress
        {
            get { return deviceAddress; }
            set { deviceAddress = value; }
        }


    }
}
