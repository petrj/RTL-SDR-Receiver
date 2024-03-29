﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class Peak : IComparable
    {
        public int Index { get; set; } = -1;
        public double Value { get; set; } = 0;

        public int CompareTo(object obj)
        {
            return (obj as Peak).Value.CompareTo(Value);
        }

        public override string ToString()
        {
            return $"Peak: Index: {Index}, Value: {Value}";
        }
    }
}
