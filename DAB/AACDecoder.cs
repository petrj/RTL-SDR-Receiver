using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace RTLSDR.DAB
{
    public class AACDecoder
    {
        const string libPath = "libfaad2";

        [DllImport(libPath)]
        public static extern IntPtr NeAACDecOpen();

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint buffer_size, out uint samplerate, out uint channels);

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecDecode(IntPtr hpDecoder, out AACDecFrameInfo hInfo, byte[] buffer, int buffer_size);

        [DllImport(libPath)]
        public static extern void NeAACDecClose(IntPtr hDecoder);

        private IntPtr _hDecoder;
        uint _samplerate;
        uint _channels;

        public void Open(AACSuperFrameFormatDecStruct format)
        {
            _hDecoder = NeAACDecOpen();
            if (_hDecoder == IntPtr.Zero)
            {
                Console.WriteLine("Nepodařilo se otevřít dekodér.");
                return;
            }

            var asc_len = 0;
            var asc = new byte[7];

            var coreSrIndex = format.dac_rate ? (format.sbr_flag ? 6 : 3) : (format.sbr_flag ? 8 : 5);  // 24/48/16/32 kHz
            var coreChConfig = format.aac_channel_mode ? 2 : 1;
            var extensionSrIndex = format.dac_rate ? 3 : 5;    // 48/32 kHz

            asc[asc_len++] = Convert.ToByte(0b00010 << 3 | coreSrIndex >> 1);
            asc[asc_len++] = Convert.ToByte( (coreSrIndex & 0x01) << 7 | coreChConfig << 3 | 0b100);

            if (format.sbr_flag)
            {
                // add SBR
                asc[asc_len++] = 0x56;
                asc[asc_len++] = 0xE5;
                asc[asc_len++] = Convert.ToByte(0x80 | (extensionSrIndex << 3));

                if (format.ps_flag)
                {
                    // add PS
                    asc[asc_len - 1] |= 0x05;
                    asc[asc_len++] = 0x48;
                    asc[asc_len++] = 0x80;
                }
            }

            int result = NeAACDecInit(_hDecoder, asc, (uint)asc_len, out _samplerate, out _channels);

            if (result != 0)
            {
                Console.WriteLine("Chyba inicializace dekodéru: " + result);
                NeAACDecClose(_hDecoder);
                return;
            }
        }

        public byte[] DecodeAAC(byte[] aacData)
        {
            byte[] pcmData = null;

            //uint pcmBufferSize = 4096;
            //IntPtr pcmBuffer = Marshal.AllocHGlobal((int)pcmBufferSize); // Alokace paměti pro PCM data

            byte[] output = new byte[4096];
            uint output_size;

            AACDecFrameInfo frameInfo = new AACDecFrameInfo();

            // this always returns error 15!
            var resultPtr = NeAACDecDecode(_hDecoder, out frameInfo, aacData, aacData.Length);

            if (frameInfo.error == 0)
            {
                // TODO: get PCM data from resultPtr
            }

            return pcmData;
        }

        public void Close()
        {
            // Uzavření dekodéru
            NeAACDecClose(_hDecoder);

            Console.WriteLine("Dekódování dokončeno.");
        }
    }

}
