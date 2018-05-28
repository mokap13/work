using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntBUSAdapter.IntbusInterfaces
{
    [Serializable]
    public abstract class IntbusInterface:ICloneable
    {
        protected int number;
        protected string name;
        public int Number => number;
        public string Name => name;

        public abstract object Clone();
    }
}
