using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter
{
    public class Device
    {
        public List<byte> Preambula { get; set; }
        public List<byte> IntBusAddedData { get; set; }
    }
}
