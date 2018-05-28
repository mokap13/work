using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter.IntbusInterfaces
{
    [Serializable]
    public class OWI : IntbusInterface
    {
        public OWI()
        {
            this.number = 0b101;
            this.name = "OWI";
        }
        public override object Clone()
        {
            return new OWI
            {
                name = this.name,
                number = this.number
            };
        }
    }
}
