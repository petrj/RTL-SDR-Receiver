using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class GainValue
    {
        public int? Value { get; set; } = null;

        public GainValue(int value)
        {
            Value = value;
        }

        public GainValue()
        {
            Value = null;
        }

        public override string ToString()
        {
            if (!Value.HasValue)
            {
                return "Auto";
            }
            else
            {
                return Value.Value.ToString();
            }
        }
    }
}
