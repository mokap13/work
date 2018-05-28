using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter.IntbusInterfaces
{
    [Serializable]
    public class SPI : IntbusInterface
    {
        public SPI()
        {
            this.number = 0b011;
            this.name = "SPI";
        }
        public override object Clone()
        {
            return new SPI
            {
                name = this.name,
                number = this.number
            };
        }
    }
}
