using System;

namespace RTLSDR.DAB
{
    public class MSCStreamDescription : MSCDescription
    {
        public uint SubChId { get; set; }  // (Sub-channel Identifier) : this 6-bit field shall identify the sub-channel in which the service
    }
}
