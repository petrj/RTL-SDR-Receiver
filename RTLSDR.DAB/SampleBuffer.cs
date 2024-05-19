using System;
using System.Collections.Generic;
using System.Threading;

namespace RTLSDR.DAB
{
    public class SampleBuffer
    {
        private Queue<byte> _byteQueue { get; set; } = new Queue<byte>();
        private object _lock = new object();

        public int TotalSamplesRead { get; set; } = 0;

        public void AddBytes(byte[] data, int count)
        {
            lock (_lock)
            {
                for (var i=0;i<count;i++)
                {
                    _byteQueue.Enqueue(data[i]);
                }
            }
        }

        public byte[] GetBytes(int count, int msTimeOut = 1000)
        {
            var res = new byte[count];
            var samplesFound = false;

            var getStart = DateTime.Now;

            while (!samplesFound)
            {
                lock (_lock)
                {
                    if (_byteQueue.Count >= count)
                    {
                        for (var i = 0; i < count; i++)
                        {
                            res[i] = _byteQueue.Dequeue();
                        }

                        TotalSamplesRead += count;
                        return res;
                    }
                    else
                    {
                        // no sample in buffer
                    }
                }

                Thread.Sleep(300);

                var span = DateTime.Now - getStart;
                if (span.TotalMilliseconds > msTimeOut)
                {
                    break;
                }
            }

            return null; // no samples found
        }
    }
}
