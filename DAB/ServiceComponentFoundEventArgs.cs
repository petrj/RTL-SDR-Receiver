using System;

namespace RTLSDR.DAB
{
    public class ServiceComponentFoundEventArgs : EventArgs
    {
        public DABService ServiceComponent { get; set; }
    }
}
