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

namespace RTLSDR.Audio
{

    using System;
    using System.IO;
    using System.Threading;
    using LibVLCSharp.Shared;

    public class PcmPointerInput : MediaInput
    {
        private readonly MemoryStream _buffer = new MemoryStream();
        private readonly object _lock = new object();

        // --- Core LibVLC Methods ---

        public override bool Open(out ulong res)
        {
            lock (_lock)
            {
                _buffer.SetLength(0); // Clear buffer on open
            }
            res = 0; // Unknown length
            return true; // Success
        }

        /// <summary>
        /// The core method: Reads raw PCM data into the memory address (nint buffer) provided by LibVLC.
        /// </summary>
        public override int Read(nint buffer, uint len)
        {
            Console.WriteLine($"LibVLC requested {len} bytes of PCM data.");
            lock (_lock)
            {
                if (_buffer.Length == 0)
                {
                    return 0; // No data available, LibVLC will wait.
                }


                Console.WriteLine($"returning data");


                // 1. Determine how much data to read (min of requested length and available length)
                int bytesToRead = (int)Math.Min(len, _buffer.Length - _buffer.Position);

                // 2. Read the necessary data *from* our internal buffer into a temporary managed array.
                // This is required because MemoryStream.Read needs a byte[] target.
                byte[] tempBuffer = new byte[bytesToRead];
                int bytesRead = _buffer.Read(tempBuffer, 0, bytesToRead);

                // 3. COPY the data from the managed array (tempBuffer) *to* the unmanaged pointer (buffer).
                // This is the key difference from the simple byte[] overload.
                Marshal.Copy(tempBuffer, 0, buffer, bytesRead);

                // 4. Handle end-of-stream logic
                if (_buffer.Position == _buffer.Length)
                {
                    _buffer.SetLength(0);
                    _buffer.Position = 0;
                }

                return bytesRead;
            }
        }

        public override void Close()
        {
            lock (_lock)
            {
                _buffer.SetLength(0);
            }
        }

        // --- Data Feeding Method (Called by your application) ---

        /// <summary>
        /// Pushes a new chunk of raw PCM data into the internal buffer.
        /// </summary>
        public void PushData(byte[] data)
        {
            lock (_lock)
            {
               // Console.WriteLine($"Pushing {data.Length} bytes of PCM data to PcmPointerInput.");
                long originalPosition = _buffer.Position;
                _buffer.Position = _buffer.Length;
                _buffer.Write(data, 0, data.Length);
                _buffer.Position = originalPosition;
            }
        }

        // Optional: Return -1 for live streams that don't support seeking.
        public override bool Seek(ulong offset)
        {
            return false;
        }
    }

    public class VLCSoundAudioPlayer : IRawAudioPlayer
    {
        private MemoryStream _stream;
        private MediaPlayer _mediaPlayer;
        private Media _media;
        private LibVLC _libVLC;
        private PcmPointerInput _pcmInput;
        //private BalanceBuffer _ballanceBuffer;

        public void Init(AudioDataDescription audioDescription, ILoggingService loggingService)
        {
            Core.Initialize();
            _libVLC = new LibVLC(enableDebugLogs: true);
            _stream = new MemoryStream();

            var mediaOptions = new[] {
                ":demux=rawaud",
                $":rawaud-channels={audioDescription.Channels}",
                $":rawaud-samplerate={audioDescription.SampleRate}",
                $":live-caching=50",
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

