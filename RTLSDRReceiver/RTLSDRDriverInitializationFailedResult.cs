﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class RTLSDRDriverInitializationFailedResult
    {
        public int ErrorId { get; set; }
        public int ExceptionCode { get; set; }
        public string DetailedDescription { get; set; }
    }
}
