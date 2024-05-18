using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AACDecFrameInfo
    {
#if OS_WINDOWS
        public uint bytesconsumed;
        public uint samples;
#elif OS_WINDOWS32
        public uint bytesconsumed;
        public uint samples;
#else
        public ulong bytesconsumed;
        public ulong samples;
#endif

        public char channels;
        public char error;

#if OS_WINDOWS
        public int samplerate;
#elif OS_WINDOWS32
        public int samplerate;
#else
        public long samplerate;
#endif

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

