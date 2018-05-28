using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter.IntbusInterfaces
{
    [Serializable]
    public class UART0 : IntbusInterface
    {
        public UART0()
        {
            this.number = 0b001;
            this.name = "UART0";
        }
        public override object Clone()
        {
            return new UART0
            {
                name = this.name,
                number = this.number
            };
        }
    }
}
