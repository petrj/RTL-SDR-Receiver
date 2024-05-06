using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AACDecConfiguration
    {
        public byte defObjectType;
        public uint defSampleRate;
        public byte outputFormat;
        public byte downMatrix;
        public byte useOldADTSFormat;
        public byte dontUpSampleImplicitSBR;
    }
}
