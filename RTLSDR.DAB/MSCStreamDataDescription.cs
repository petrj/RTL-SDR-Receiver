using System;

namespace RTLSDR.DAB
{
    public class MSCStreamDataDescription : MSCStreamDescription
    {
        public uint DataServiceComponentType { get; set; }   //  ETSI TS 101 756 [3], table 2b.
    }
}
