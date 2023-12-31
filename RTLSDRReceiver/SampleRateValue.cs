﻿using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDRReceiver
{
    public class SampleRateValue
    {
        public int Value { get; set; } = 1000000;

        public SampleRateValue(int value)
        {
            Value = value;
        }

        public override string ToString()
        {
            var val = Value / 1000;

            return $"{val.ToString("N0")} KHz";
        }
    }
}
