using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AACDecFrameInfo
    {
        /*
        public ulong bytesconsumed;  // 8
        public ulong samples;        // 8
        public byte channels;        // 1
        public byte error;           // 1
        public ulong samplerate;     // 8

        // SBR: 0: off, 1: on; upsample, 2: on; downsampled, 3: off; upsampled
        public byte sbr;             // 1

        // MPEG-4 ObjectType
        public byte object_type;     // 1

        // AAC header type; MP4 will be signalled as RAW also
        public byte header_type;     // 1

        // multichannel configuration
        public byte num_front_channels; // 1
        public byte num_side_channels;  // 1
        public byte num_back_channels;  // 1
        public byte num_lfe_channels;   // 1

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] channel_position; // 64

        // PS: 0: off, 1: on
        public byte ps;                 // 1
        //                             -----
        //                                98
        */

        public Int32 bytesconsumed;
        public Int32 samples;
        public char channels;
        public char error;
        public Int32 samplerate;
        public char sbr;
        public char object_type;
        public char header_type;
        public char num_front_channels;
        public char num_side_channels;
        public char num_back_channels;
        public char num_lfe_channels;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public char[] channel_position;
        public char ps;
    }
}
