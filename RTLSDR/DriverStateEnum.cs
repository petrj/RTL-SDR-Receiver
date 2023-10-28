using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR
{
    public enum DriverStateEnum
    {
        NotInitialized = 0,
        Initialized = 1,
        Connected = 2,
        Error = 3
    }
}
