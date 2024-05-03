using System;
using System.Runtime.InteropServices;

namespace RTLSDR.DAB
{
    public class AACDecoder
    {
        // Importování funkce NeAACDecDecode z knihovny faad2.so pro Android
      /*  [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecOpen();

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint size, out uint samplerate, out uint channels);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecDecode(IntPtr hDecoder, IntPtr hInfo, byte[] buffer, uint size, IntPtr pcmBuffer, uint maxSize, out uint sample);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NeAACDecClose(IntPtr hDecoder);

        private IntPtr hDecoder;      
        */
        public void Open()
        {
         /*   // Otevření dekodéru
            hDecoder = NeAACDecOpen();
            if (hDecoder == IntPtr.Zero)
            {
                Console.WriteLine("Nepodařilo se otevřít dekodér.");
                return;
            }

            // Inicializace dekodéru a získání informací o vzorkovací frekvenci a počtu kanálů
            uint samplerate, channels;
            byte[] buffer = new byte[1024];
            int result = NeAACDecInit(hDecoder, buffer, (uint)buffer.Length, out samplerate, out channels);
            if (result != 0)
            {
                Console.WriteLine("Chyba inicializace dekodéru: " + result);
                NeAACDecClose(hDecoder);
                return;
            }*/
        }

        public byte[] DecodeAAC(byte[] aacData)
        {
            /*     // Dekódování AAC dat a získání PCM
                 uint pcmBufferSize = 4096;
                 IntPtr pcmBuffer = Marshal.AllocHGlobal((int)pcmBufferSize); // Alokace paměti pro PCM data
                 uint sample;
                 var result = NeAACDecDecode(hDecoder, IntPtr.Zero, aacData, (uint)aacData.Length, pcmBuffer, pcmBufferSize, out sample);
                 if (result != 0)
                 {
                     Console.WriteLine("Chyba při dekódování: " + result);
                     NeAACDecClose(hDecoder);
                     Marshal.FreeHGlobal(pcmBuffer); // Uvolnění paměti
                     return null;
                 }

                 byte[] pcmData = null;

                 if (sample > 0)
                 {
                     // Kopírování dat z IntPtr do byte[]
                     pcmData = new byte[pcmBufferSize];
                     Marshal.Copy(pcmBuffer, pcmData, 0, (int)pcmBufferSize);
                 }

                 // Uvolnění paměti
                 Marshal.FreeHGlobal(pcmBuffer);

                 return pcmData;*/


            return null;
        }

        public void Close()
        {
            // Uzavření dekodéru
            //NeAACDecClose(hDecoder);

            Console.WriteLine("Dekódování dokončeno.");
        }
    }

}
