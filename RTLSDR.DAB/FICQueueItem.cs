using System;
namespace RTLSDR.DAB
{
    public struct FICQueueItem
    {
        public int FicNo{ get; set; }
        public sbyte[] Data { get; set; }
    }
}
