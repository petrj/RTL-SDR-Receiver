using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class DriverTypeValue
    {
        public DriverTypeEnum Value { get; set; } = DriverTypeEnum.Testing_Driver;

        public DriverTypeValue(DriverTypeEnum value)
        {
            Value = value;
        }

        public override string ToString()
        {
            switch (Value)
            {
                case DriverTypeEnum.Testing_Driver: return "Testing driver";
                case DriverTypeEnum.RTLSDR_Android: return "RTL Android";
                case DriverTypeEnum.RTLSDR_TCP: return "RTL TCP";
            }

            return string.Empty;
        }
    }
}
