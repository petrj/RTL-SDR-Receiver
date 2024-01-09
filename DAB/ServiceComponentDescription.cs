using System;
namespace DAB
{
    public class ServiceComponentDescription
    {
        public MSCStreamAudioDescription MSCStreamAudioDesc { get; set; }
        public MSCStreamDataDescription MSCStreamDataDesc { get; set; }
        public MSCPacketDataDescription MSCPacketData { get; set; }

        public ServiceComponentDescription()
        {
            MSCPacketData = new MSCPacketDataDescription();
            MSCStreamDataDesc = new MSCStreamDataDescription();
            MSCStreamAudioDesc = new MSCStreamAudioDescription();
        }
    }
}
