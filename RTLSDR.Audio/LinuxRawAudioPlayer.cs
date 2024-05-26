using NAudio.Wave;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.Audio
{
    public class LinuxRawAudioPlayer : IRawAudioPlayer
    {
        const int SND_PCM_STREAM_PLAYBACK = 0;
        const int SND_PCM_FORMAT_S16_LE = 2;

        const int SND_PCM_ACCESS_MMAP_INTERLEAVED = 0;
        const int SND_PCM_ACCESS_MMAP_NONINTERLEAVED = 1;
        const int SND_PCM_ACCESS_MMAP_COMPLEX = 2;
        const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;
        const int SND_PCM_ACCESS_RW_NONINTERLEAVED = 4;

        public void Init(AudioDataDescription audioDescription)
        {

            int err;

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
        }

        public void Play()
        {

        }

        public void AddPCM(byte[] data)
        {
            //IntPtr pcmDataPtr = Marshal.AllocHGlobal(ed.Data.Length);
            //Marshal.Copy(ed.Data, 0, pcmDataPtr, ed.Data.Length);

            //// Write PCM data to the audio device
            //snd_pcm_writei(_pcm, pcmDataPtr, ed.Data.Length);

            //// Free unmanaged memory
            //Marshal.FreeHGlobal(pcmDataPtr);
        }

        public void Stop()
        {
            // TODO
        }
    }
}
