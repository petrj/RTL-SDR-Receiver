using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AACDecConfiguration
    {
        public byte defObjectType;
#if _WINDOWS
        public uint defSampleRate;
#else
        public ulong defSampleRate;
#endif
        public byte outputFormat;
        public byte downMatrix;
        public byte useOldADTSFormat;
        public byte dontUpSampleImplicitSBR;
    }
}
