using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABServicePlayedEventArgs : EventArgs
    {
        public DABService Service { get; set; }
        public DABSubChannel SubChannel { get; set; }
    }
}
