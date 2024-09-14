using NAudio.Wave;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using System.IO;

namespace RTLSDR.Audio
{
    public class VLCSoundAudioPlayer : IRawAudioPlayer
    {
        private MemoryStream _stream;
        private MediaPlayer _mediaPlayer;
        private Media _media;
        private LibVLC _libVLC;

        public void Init(AudioDataDescription audioDescription)
        {
            _libVLC = new LibVLC(enableDebugLogs: true);
            _stream = new MemoryStream();

            _media = new Media(_libVLC, new StreamMediaInput(_stream));

            _mediaPlayer = new MediaPlayer(_media);
            _mediaPlayer.SetAudioFormat("S16N", (uint)audioDescription.SampleRate, (uint)audioDescription.Channels);
        }

        public void Play()
        {
            _mediaPlayer.Play();
        }

        public void AddPCM(byte[] data)
        {
            _stream.Write(data, 0, data.Length);
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
        }
    }
}
