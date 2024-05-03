using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    public class AACDecoder
    {
        // Importování funkce NeAACDecDecode z knihovny faad2.so pro Android
        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecOpen();

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint size, out uint samplerate, out uint channels);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecDecode(IntPtr hDecoder, IntPtr hInfo, byte[] buffer, uint size, IntPtr pcmBuffer, uint maxSize, out uint sample);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
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

            // Inicializace dekodéru a získání informací o vzorkovací frekvenci a počtu kanálů
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
             // Dekódování AAC dat a získání PCM
             uint pcmBufferSize = 4096;
             IntPtr pcmBuffer = Marshal.AllocHGlobal((int)pcmBufferSize); // Alokace paměti pro PCM data
             
             uint sample;

            var frameInfo = new AACDecFrameInfo();
            frameInfo.channel_position = new byte[64];
            var structureSize = Marshal.SizeOf(typeof(AACDecFrameInfo)); // should be 98!
            IntPtr ptr = Marshal.AllocHGlobal(structureSize);
            Marshal.StructureToPtr(frameInfo, ptr, true);

            var result = NeAACDecDecode(_hDecoder, ptr, aacData, (uint)aacData.Length, pcmBuffer, pcmBufferSize, out sample);
             if (result != 0)
             {
                 Console.WriteLine("Chyba při dekódování: " + result);
                 NeAACDecClose(_hDecoder);
                 Marshal.FreeHGlobal(pcmBuffer); // Uvolnění paměti
                 return null;
             }


            frameInfo = Marshal.PtrToStructure<AACDecFrameInfo>(ptr);

            byte[] pcmData = null;

             if (sample > 0)
             {
                 // Kopírování dat z IntPtr do byte[]
                 pcmData = new byte[pcmBufferSize];
                 Marshal.Copy(pcmBuffer, pcmData, 0, (int)pcmBufferSize);
             }

             // Uvolnění paměti
             Marshal.FreeHGlobal(pcmBuffer);

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
