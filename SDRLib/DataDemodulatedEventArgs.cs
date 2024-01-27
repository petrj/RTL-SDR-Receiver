using System;
namespace SDRLib
{
    public class DataDemodulatedEventArgs : EventArgs
    {
        public byte[] Data { get; set; }
    }
}
