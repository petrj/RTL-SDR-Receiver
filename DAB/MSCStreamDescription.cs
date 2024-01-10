using System;
namespace DAB
{
    public class MSCStreamDescription : MSCDescription
    {
        public uint SubChId { get; set; }  // (Sub-channel Identifier) : this 6-bit field shall identify the sub-channel in which the service

        public MSCStreamDescription()
        {

        }
    }
}
