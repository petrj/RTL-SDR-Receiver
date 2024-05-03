using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    public struct AACDecFrameInfo
    {
        ulong bytesconsumed;
        ulong samples;
        byte channels;
        byte error;
        ulong samplerate;

        /* SBR: 0: off, 1: on; upsample, 2: on; downsampled, 3: off; upsampled */
        byte sbr;

        /* MPEG-4 ObjectType */
        byte object_type;

        /* AAC header type; MP4 will be signalled as RAW also */
        byte header_type;

        /* multichannel configuration */
        byte num_front_channels;
        byte num_side_channels;
        byte num_back_channels;
        byte num_lfe_channels;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        byte[] channel_position; // 64 

        /* PS: 0: off, 1: on */
        byte ps;
    }
}
