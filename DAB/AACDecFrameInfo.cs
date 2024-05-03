using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    public struct AACDecFrameInfo
    {
        public ulong bytesconsumed;  // 8
        public ulong samples;        // 8
        public byte channels;        // 1
        public byte error;           // 1
        public ulong samplerate;     // 8

        /* SBR: 0: off, 1: on; upsample, 2: on; downsampled, 3: off; upsampled */
        public byte sbr;             // 1

        /* MPEG-4 ObjectType */
        public byte object_type;     // 1

        /* AAC header type; MP4 will be signalled as RAW also */
        public byte header_type;     // 1

        /* multichannel configuration */
        public byte num_front_channels; // 1
        public byte num_side_channels;  // 1
        public byte num_back_channels;  // 1
        public byte num_lfe_channels;   // 1

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] channel_position; // 64 

        /* PS: 0: off, 1: on */
        public byte ps;                 // 1 
        //                             -----
        //                                98
    }
}
