using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter.IntbusInterfaces
{
    [Serializable]
    public class I2C : IntbusInterface
    {
        public I2C()
        {
            this.number = 0b100;
            this.name = "I2C";
        }
        public override object Clone()
        {
            return new I2C
            {
                name = this.name,
                number = this.number
            };
        }
    }
}
