using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class ModeValue
    {
        public ModeEnum Value { get; set; } = ModeEnum.FM;

        public ModeValue(ModeEnum value)
        {
            Value = value;
        }

        public override string ToString()
        {
            switch (Value)
            {
                case ModeEnum.FM: return "FM radio";
                case ModeEnum.DAB: return "DAB+";
            }

            return string.Empty;
        }
    }
}
