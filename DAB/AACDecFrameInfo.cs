using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AACDecFrameInfo
    {
        public uint bytesconsumed;
        public uint samples;
        public char channels;
        public char error;
        public uint samplerate;
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
