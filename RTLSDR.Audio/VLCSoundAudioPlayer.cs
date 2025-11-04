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
        private MemoryStream _stream;
        private MediaPlayer _mediaPlayer;
        private Media _media;
        private LibVLC _libVLC;
        private PcmPointerInput _pcmInput;

        public void Init(AudioDataDescription audioDescription, ILoggingService loggingService)
        {
            Core.Initialize();
            _libVLC = new LibVLC(enableDebugLogs: true);
            _stream = new MemoryStream();

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
            _pcmInput = new PcmPointerInput();

            _media = new Media(_libVLC, _pcmInput, mediaOptions);

            _mediaPlayer = new MediaPlayer(_media);
            _mediaPlayer.Volume = 100;


            // _ballanceBuffer = new BalanceBuffer(loggingService, (data) =>
            // {
            //     if (data == null)
            //         return;

            //     _stream.Write(data, 0, data.Length);
            // });

            //_ballanceBuffer.SetAudioDataDescription(audioDescription);
        }

        public bool PCMProcessed
        {
            get
            {
                return true; // no Balance buffer
            }
        }

        public void Play()
        {
            _mediaPlayer.Play();
        }

        public void AddPCM(byte[] data)
        {
            //_ballanceBuffer.AddData(data);
            //_stream.Write(data);

            _pcmInput.PushData(data);
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
        }
    }
}

