using NAudio.Wave;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace RTLSDR.Audio
{
    public class AlsaSoundAudioPlayer : IRawAudioPlayer
    {
    [   DllImport("libasound", CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);

        [DllImport("libasound", CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_set_params(IntPtr pcm, int format, int access, int channels, int rate, int soft_resample, int latency);

        [DllImport("libasound", CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_writei(IntPtr pcm, IntPtr buffer, int size);

        [DllImport("libasound", CallingConvention = CallingConvention.Cdecl)]
        private static extern int snd_pcm_close(IntPtr pcm);

        IntPtr _pcm = IntPtr.Zero;

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
            if ((err = snd_pcm_open(out _pcm, "default", SND_PCM_STREAM_PLAYBACK, 0)) < 0)
            {
                Console.WriteLine("Playback open error ");
                return;
            }
            //// Set PCM parameters: format = 16-bit little-endian
            if ((err = snd_pcm_set_params(_pcm, SND_PCM_FORMAT_S16_LE, SND_PCM_ACCESS_RW_INTERLEAVED, audioDescription.Channels, audioDescription.BitsPerSample, 0, 500000)) < 0)
            {
                Console.WriteLine("Playback open error ");
                return;
            }
        }

        public void Play()
        {

        }

        public void AddPCM(byte[] data)
        {
            /* when uncommented, AAC thread is stopping without raising exception!
            IntPtr pcmDataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, pcmDataPtr, data.Length);

            // Write PCM data to the audio device
            var res = snd_pcm_writei(_pcm, pcmDataPtr, data.Length);
            // Free unmanaged memory
            Marshal.FreeHGlobal(pcmDataPtr);

            if (res != 0)
            {
                Console.WriteLine("Playback error");
            }
            */
        }

        public void Stop()
        {
            snd_pcm_close(_pcm);
        }
    }
}
