﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class ServiceComponentGlobalDefinitionFoundEventArgs : EventArgs
    {
        public DABServiceComponentGlobalDefinition ServiceGlobalDefinition { get; set; }
    }
}
