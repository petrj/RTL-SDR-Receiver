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
            try
            {
                if (len > MaxDataRequestSize)
                    len = MaxDataRequestSize;

                int totalBytes = 0;
                Span<byte> tempSpan = stackalloc byte[(int)Math.Min(len, 65536)]; // small temp buffer

                while (totalBytes < len)
                {
                    if (_buffer.TryTake(out var chunk))
                    {
                        int toCopy = Math.Min(chunk.Length, (int)(len - totalBytes));

                        // Copy safely from chunk into unmanaged buffer
                        Marshal.Copy(chunk, 0, buffer + totalBytes, toCopy);

                        totalBytes += toCopy;

                        if (totalBytes >= len)
                            break;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }

                return totalBytes > 0 ? totalBytes : 100; // never return 0
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VLCMediaInput Read exception: {ex}");
                try
                {
                    var dummy = new byte[100];
                    Marshal.Copy(dummy, 0, buffer, dummy.Length);
                    return dummy.Length;
                }
                catch { return 100; } // fallback
            }
        }

        /*
        public override int Read(nint buffer, uint len)
        {
            //Console.WriteLine($"LibVLC requested {len} bytes of PCM data");

            try
            {
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

                var bytes = byteList.ToArray();

                Marshal.Copy(bytes, 0, buffer, byteList.Count);
                return byteList.Count;
            } catch (Exception ex)
            {
                Console.WriteLine($"VLCMediaInput Read exception: {ex}");

                // never return 0, vlc will cut off communication
                Marshal.Copy(new byte[100], 0, buffer, 100);
                return 100;
            }
        }
        */
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