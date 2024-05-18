using System;
using System.Diagnostics;
using System.IO;
using LoggerService;
using FMDAB.Core;
using RTLSDR.FM;
using RTLSDR.DAB;
using RTLSDR.RTLSDRFMDABRadioConsoleCommon;

namespace RTLSDR.FMDAB.Console32
{
    public class MainClass
    {

        public static void Main(string[] args)
        {
            // test:
            //var aacDecoder = new AACDecoder(logger);
            //var decodeTest = aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");

            //const int SND_PCM_STREAM_PLAYBACK = 0;
            //const int SND_PCM_FORMAT_S16_LE = 2;

            //const int SND_PCM_ACCESS_MMAP_INTERLEAVED = 0;
            //const int SND_PCM_ACCESS_MMAP_NONINTERLEAVED = 1;
            //const int SND_PCM_ACCESS_MMAP_COMPLEX = 2;
            //const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;
            //const int SND_PCM_ACCESS_RW_NONINTERLEAVED = 4;

            //int err;

            //// Open PCM device for playback
            //if ((err = snd_pcm_open(out _pcm, "default", SND_PCM_STREAM_PLAYBACK, 0)) < 0)
            //{
            //    Console.WriteLine("Playback open error ");
            //    return;
            //}
            //// Set PCM parameters: format = 16-bit little-endian
            //if ((err = snd_pcm_set_params(_pcm, SND_PCM_FORMAT_S16_LE, SND_PCM_ACCESS_RW_INTERLEAVED, 2, 48000, 0, 500000)) < 0)
            //{
            //    Console.WriteLine("Playback open error ");
            //    return;
            //}

            var app = new ConsoleApp("RTLSDRFMDABRadio32");
            app.OnDemodulated += Program_OnDemodulated;
            app.Run(args);

        }

        private static void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                //IntPtr pcmDataPtr = Marshal.AllocHGlobal(ed.Data.Length);
                //Marshal.Copy(ed.Data, 0, pcmDataPtr, ed.Data.Length);

                //// Write PCM data to the audio device
                //snd_pcm_writei(_pcm, pcmDataPtr, ed.Data.Length);

                //// Free unmanaged memory
                //Marshal.FreeHGlobal(pcmDataPtr);
            }
        }
    }
}
