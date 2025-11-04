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
    using System;
    using System.IO;
    using System.Threading;
    using LibVLCSharp.Shared;

    public class PcmPointerInput : MediaInput
    {
        private readonly BlockingCollection<byte> _buffer = new BlockingCollection<byte>();

        public override bool Open(out ulong res)
        {
            res = 0; 
            return true;
        }

        /// <summary>
        /// The core method: Reads raw PCM data into the memory address (nint buffer) provided by LibVLC.
        /// </summary>
        public override int Read(nint buffer, uint len)
        {
            Console.WriteLine($"LibVLC requested {len} bytes of PCM data.");

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

        /// <summary>
        /// Pushes a new chunk of raw PCM data into the internal buffer.
        /// </summary>
        public void PushData(byte[] data)
        {
            foreach (var b in data)
            {
                _buffer.Add(b);
            }
        }

        // Optional: Return -1 for live streams that don't support seeking.
        public override bool Seek(ulong offset)
        {
            return false;
        }
    }
}    