using System;
namespace DAB
{
    public class MSCStreamAudioDescription : MSCStreamDescription
    {
        public uint AudioServiceComponentType { get; set; }  //  ETSI TS 101 756 [3], table 2a.  
    }
}
