using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using LoggerService;
using SDRLib;

namespace FM
{
    public class FMDemodulator : IDemodulator
    {
        private object _lock = new object();
        public int _bufferSize = 100 * 1024; // 100 kb demodulation buffer
        public byte[] _buffer = null;
        public short[] _demodBuffer = null;

        // https://github.com/osmocom/rtl-sdr/blob/master/src/rtl_fm.c

        short pre_r = 0;
        short pre_j = 0;
        short now_r = 0;
        short now_j = 0;
        int prev_index = 0;

        public int Samplerate { get; set; } = 96000;

        public int BufferSize
        {
            get
            {
                return _bufferSize;
            }
            set
            {
                _bufferSize = value;
                ResizeBuffer();
            }
        }

        private BackgroundWorker _worker = null;
        private ILoggingService _loggingService;

        private Queue<byte> _queue = new Queue<byte>();
        private DateTime _lastQueueSizeNotifyTime = DateTime.MinValue;

        public event EventHandler OnDemodulated;
        public delegate void OnDemodulatedEventHandler(object sender, DataDemodulatedEventArgs e);

        private void ResizeBuffer()
        {
            _buffer = new byte[_bufferSize];
            _demodBuffer = new short[_bufferSize];
        }

        public FMDemodulator(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            ResizeBuffer();

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork;;
            _worker.RunWorkerAsync();
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info($"Starting FM demodulator worker thread`");
            var processed = false;

            while (!_worker.CancellationPending)
            {
                processed = false;
                lock (_lock)
                {
                    if (_queue.Count >= BufferSize)
                    {
                        for (var i = 0; i < BufferSize; i++)
                        {
                            _buffer[i] = _queue.Dequeue();
                        }

                        processed = true;
                    }
                }

                if ((DateTime.Now - _lastQueueSizeNotifyTime).TotalSeconds > 5)
                {
                    _loggingService.Info($"<--------------------------------------- FM queue size: {(_queue.Count / 1024).ToString("N0")} KB");
                    _lastQueueSizeNotifyTime = DateTime.Now;
                }

                if (!processed)
                {
                    Thread.Sleep(300);
                } else
                {
                    if (OnDemodulated != null)
                    {
                        var arg = new DataDemodulatedEventArgs();
                        var lowPassedDataLength = LowPassWithMove(_buffer, _demodBuffer, BufferSize, Samplerate, -127);

                        var demodulatedDataMonoLength = FMDemodulate(_demodBuffer, lowPassedDataLength, false);

                        arg.Data = GetBytes(_demodBuffer, demodulatedDataMonoLength);

                        OnDemodulated(this, e);
                    }
                }
            }

            _loggingService.Info($"FM demodulator worker thread finished`");
        }

        private static byte[] GetBytes(short[] data, int count)
        {
            var res = new byte[count * 2];

            var pos = 0;
            for (int i = 0; i < count; i++)
            {
                var dataToWrite = BitConverter.GetBytes(data[i]);
                res[pos + 0] = (byte)dataToWrite[0];
                res[pos + 1] = (byte)dataToWrite[1];
                pos += 2;
            }

            return res;
        }

        public void AddSamples(byte[] IQData, int length)
        {
            lock (_lock)
            {
                for (var i=0;i<length;i++)
                {
                    _queue.Enqueue(IQData[i]);
                }
            }
        }

        public static short PolarDiscriminant(int ar, int aj, int br, int bj)
        {
            double angle;

            // multiply
            var cr = ar * br - aj * (-bj);
            var cj = aj * br + ar * (-bj);

            angle = Math.Atan2(cj, cr);
            return (short)(angle / 3.14159 * (1 << 14));
        }

        private static int fast_atan2(int y, int x)
        /* pre scaled for int16 */
        {
            int yabs, angle;
            int pi4 = (1 << 12), pi34 = 3 * (1 << 12);  // note pi = 1<<14
            if (x == 0 && y == 0)
            {
                return 0;
            }
            yabs = y;
            if (yabs < 0)
            {
                yabs = -yabs;
            }
            if (x >= 0)
            {
                angle = pi4 - pi4 * (x - yabs) / (x + yabs);
            }
            else
            {
                angle = pi34 - pi4 * (x + yabs) / (yabs - x);
            }
            if (y < 0)
            {
                return -angle;
            }
            return angle;
        }

        private static short FastPolarDiscriminant(int ar, int aj, int br, int bj)
        {
            var cr = ar * br - aj * (-bj);
            var cj = aj * br + ar * (-bj);

            return Convert.ToInt16(fast_atan2(cj, cr));
        }

        public int FMDemodulate(short[] lp, int count, bool fast = false)
        {
            //var res = new short[lp.Length / 2];

            lp[0] = PolarDiscriminant(lp[0], lp[1], pre_r, pre_j);

            for (var i = 2; i < (count - 2); i += 2)
            {
                if (fast)
                {
                    lp[i / 2] = FastPolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
                }
                else
                {
                    lp[i / 2] = PolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
                }
            }
            pre_r = lp[lp.Length - 2];
            pre_j = lp[lp.Length - 1];

            return count / 2;
        }

        public int LowPassWithMove(byte[] iqData, short[] res, int count, double samplerate, short moveVector)
        {
            int downsample = Convert.ToInt32((1000000 / samplerate) + 1);
            int adjustedCount = count - 1;
            int i = 0, i2 = 0;
            short nr = now_r, nj = now_j;
            int pi = prev_index;

            while (i < adjustedCount)
            {
                nr += (short)(iqData[i] + moveVector);
                nj += (short)(iqData[i + 1] + moveVector);
                i += 2;
                pi++;

                if (pi >= downsample)
                {
                    res[i2++] = nr;
                    res[i2++] = nj;
                    pi = 0;
                    nr = 0;
                    nj = 0;
                }
            }

            now_r = nr;
            now_j = nj;
            prev_index = pi;

            return i2;
        }
    }
}
