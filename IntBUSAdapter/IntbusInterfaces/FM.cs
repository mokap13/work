using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter.IntbusInterfaces
{
    [Serializable]
    public class FM:IntbusInterface
    {
        public FM()
        {
            this.number = 0b010;
            this.name = "FM";
        }
        public override object Clone()
        {
            return new FM
            {
                name = this.name,
                number = this.number
            };
        }
    }
}
