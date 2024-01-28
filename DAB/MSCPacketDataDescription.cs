using System;

namespace RTLSDR.DAB
{
    public class MSCPacketDataDescription : MSCDescription
    {
        public uint ServiceComponentIdentifier { get; set; }   // this 12-bit field shall uniquely identify the service component within the ensemble
    }
}
