using System;
using LibVLCSharp.Shared;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace RTLSDR.Audio
{
    public class VLCMediaInput : MediaInput
    {
        public uint MaxDataRequestSize { get;set; } = 96000*16*2/8; // 1 s of stereo audio

        private readonly BlockingCollection<byte[]> _buffer = new BlockingCollection<byte[]>();

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
                // vlc wants to read 16 MB at the begining,
                // we have live stream, so we will return max MaxDataRequestSize bytes (default ~ 1 s of stereo data)
                // vlc will request for next data soon ...
                // vlc cut off communication when has no data!
                len = MaxDataRequestSize;
            }

            var byteList = new List<byte>();

            while (byteList.Count < len)
            {
                if (_buffer.TryTake(out var b))
                {
                    byteList.AddRange(b);
                }
                else
                {
                    Thread.Sleep(10); // wait for producer to catch up
                }
            }

            Marshal.Copy(byteList.ToArray(), 0, buffer, byteList.Count);
            return byteList.Count;
        }

        public override void Close()
        {

        }

        public void PushData(byte[] data)
        {
            //Console.WriteLine($"Feeding data: {data.Length/1000} KB");

            _buffer.Add(data);
        }

        public override bool Seek(ulong offset)
        {
            return false;
        }
    }
}