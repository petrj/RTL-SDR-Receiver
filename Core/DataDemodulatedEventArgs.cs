using System;

namespace RTLSDR.Core
{
    public class DataDemodulatedEventArgs : EventArgs
    {
        public byte[] Data { get; set; }

    }
}
