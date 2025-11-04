using System;
using LibVLCSharp.Shared;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace RTLSDR.Audio
{
    public class PcmPointerInput : MediaInput
    {
        public uint MaxDataRequestSize { get;set; } = 96000*16*2/8; // 1 s of stereo audio

        private readonly BlockingCollection<byte> _buffer = new BlockingCollection<byte>();

        public override bool Open(out ulong res)
        {
            res = 0;
            return true;
        }

        public override int Read(nint buffer, uint len)
        {
            //Console.WriteLine($"LibVLC requested {len} bytes of PCM data");

            if (len > MaxDataRequestSize)
            {
                // vlc wants to read 16 MB of dat at the beginng,
                // we have live stream, so we will return max MaxDataRequestSize bytes (default ~ 1 s of stereo data)
                // vlc will request for next data soon ...
                // when vlc got no data, it will cut of the reading
                len = MaxDataRequestSize;
            }

           // Block until at least 'len' bytes available
            var bytes = new byte[len];
            int read = 0;

            while (read < len)
            {
                if (_buffer.TryTake(out var b))
                {
                    bytes[read++] = b;
                }
                else
                {
                    Thread.Sleep(10); // wait for producer to catch up
                }
            }

            Marshal.Copy(bytes, 0, buffer, (int)len);
            return (int)len;
        }

        public override void Close()
        {
            //_buffer?.Clear();
        }

        public void PushData(byte[] data)
        {
            //Console.WriteLine($"Feeding data: {data.Length/1000} KB");

            foreach (var b in data)
            {
                _buffer.Add(b);
            }
        }

        public override bool Seek(ulong offset)
        {
            return false;
        }
    }
}