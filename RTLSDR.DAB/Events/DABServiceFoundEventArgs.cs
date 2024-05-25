using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABServiceFoundEventArgs : EventArgs
    {
        public DABService Service { get; set; }
    }
}
