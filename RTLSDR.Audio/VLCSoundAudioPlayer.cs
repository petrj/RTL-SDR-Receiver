using System;
using System.IO;
using System.Threading;
using LibVLCSharp.Shared;
using NAudio.Wave;
using LoggerService;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using System.IO;
using System.Collections.Concurrent;

namespace RTLSDR.Audio
{
    public class VLCSoundAudioPlayer : IRawAudioPlayer
    {
        private MediaPlayer? _mediaPlayer;
        private Media? _media;
        private LibVLC? _libVLC;
        private VLCMediaInput _pcmInput = new VLCMediaInput();
        private AudioDataDescription? _audioDescription;

        public void Init(AudioDataDescription audioDescription, ILoggingService loggingService)
        {
            _audioDescription = audioDescription;

            Core.Initialize();
            _libVLC = new LibVLC(
                "--quiet",
                "--no-stats",
                "--verbose=0"
            );

            var mediaOptions = new[] {
                ":demux=rawaud",
                $":rawaud-channels={audioDescription.Channels}",
                $":rawaud-samplerate={audioDescription.SampleRate}",
                ":live-caching=50",
                ":file-caching=50",
                ":clock-jitter=0",
                ":clock-synchro=0",
                ":rawaud-fourcc=s16l"
            };

            _media = new Media(_libVLC, _pcmInput, mediaOptions);

            _mediaPlayer = new MediaPlayer(_media);
            _mediaPlayer.Volume = 100;
        }

        public AudioDataDescription? GetAudioDataDescription()
        {
            return _audioDescription;
        }

        public void Play()
        {
            _mediaPlayer?.Play();
        }

        public void AddPCM(byte[] data)
        {
            _pcmInput.PushData(data);
        }

        public void Stop()
        {
            _mediaPlayer?.Stop();
        }

        public void ClearBuffer()
        {
            _pcmInput.ClearBuffer();
        }
    }
}

