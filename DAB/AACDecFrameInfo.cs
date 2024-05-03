using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    public struct AACDecFrameInfo
    {
        public ulong bytesconsumed;
        public ulong samples;
        public byte channels;
        public byte error;
        public ulong samplerate;

        /* SBR: 0: off, 1: on; upsample, 2: on; downsampled, 3: off; upsampled */
        public byte sbr;

        /* MPEG-4 ObjectType */
        public byte object_type;

        /* AAC header type; MP4 will be signalled as RAW also */
        public byte header_type;

        /* multichannel configuration */
        public byte num_front_channels;
        public byte num_side_channels;
        public byte num_back_channels;
        public byte num_lfe_channels;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] channel_position; // 64 

        /* PS: 0: off, 1: on */
        public byte ps;
    }
}
