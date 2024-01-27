using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using LoggerService;
using SDRLib;

namespace FM
{
    public class FMDemodulator : IDemodulator
    {
        public int Samplerate { get; set; } = 96000;
        public bool Emphasize { get; set; } = false;

        private object _lock = new object();
        private bool _finish = false;

        private int _bufferSize = 100 * 1024; // 100 kb demodulation buffer
        private byte[] _buffer = null;
        private short[] _demodBuffer = null;

        // https://github.com/osmocom/rtl-sdr/blob/master/src/rtl_fm.c

        private short pre_r = 0;
        private short pre_j = 0;
        private short now_r = 0;
        private short now_j = 0;
        private int prev_index = 0;

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

        public FMDemodulator(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            ResizeBuffer();

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork; ;
            _worker.RunWorkerAsync();
        }

        private void ResizeBuffer()
        {
            _buffer = new byte[_bufferSize];
            _demodBuffer = new short[_bufferSize];
        }

        public void Finish()
        {
            _finish = true;
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info($"Starting FM demodulator worker thread`");
            var processed = false;
            var processedBytesCount = 0;

            while (!_worker.CancellationPending)
            {
                processed = false;
                lock (_lock)
                {
                    if (_queue.Count >= BufferSize || _finish)
                    {
                        processedBytesCount = _finish ? _queue.Count : BufferSize;
                        for (var i = 0; i < processedBytesCount; i++)
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
                }
                else
                {
                    if (OnDemodulated != null)
                    {
                        var arg = new DataDemodulatedEventArgs();

                        if (Emphasize)
                        {
                            var lowPassedDataMonoDeemphLength = LowPassWithMove(_buffer, _demodBuffer, processedBytesCount, 170000, -127);
                            var demodulatedDataMono2Length = FMDemodulate(_demodBuffer, lowPassedDataMonoDeemphLength, true);
                            DeemphFilter(_demodBuffer, demodulatedDataMono2Length, 170000);
                            var finalBytesCount = LowPassReal(_demodBuffer, demodulatedDataMono2Length, 170000, 32000);
                            arg.Data = GetBytes(_demodBuffer, finalBytesCount);
                        }
                        else
                        {
                            var lowPassedDataLength = LowPassWithMove(_buffer, _demodBuffer, processedBytesCount, Samplerate, -127);

                            var demodulatedDataMonoLength = FMDemodulate(_demodBuffer, lowPassedDataLength, false);

                            arg.Data = GetBytes(_demodBuffer, demodulatedDataMonoLength);
                        }

                        OnDemodulated(this, arg);
                    }
                }
            }

            _loggingService.Info($"FM demodulator worker thread finished`");
        }

        public int LowPassReal(short[] lp, int count, int sampleRateOut = 170000, int sampleRate2 = 32000)
        {
            int now_lpr = 0;
            int prev_lpr_index = 0;

            int i = 0;
            int i2 = 0;

            while (i < count)
            {
                now_lpr += lp[i];
                i++;
                prev_lpr_index += sampleRate2;

                if (prev_lpr_index < sampleRateOut)
                {
                    continue;
                }

                lp[i2] = Convert.ToInt16(now_lpr / ((double)sampleRateOut / (double)sampleRate2));

                prev_lpr_index -= sampleRateOut;
                now_lpr = 0;
                i2 += 1;
            }

            return i2;
        }

        public void DeemphFilter(short[] lp, int count, int sampleRate = 170000)
        {
            var deemph_a = Convert.ToInt32(1.0 / ((1.0 - Math.Exp(-1.0 / (sampleRate * 75e-6)))));

            var avg = 0;
            for (var i = 0; i < count; i++)
            {
                var d = lp[i] - avg;
                if (d > 0)
                {
                    avg += (d + deemph_a / 2) / deemph_a;
                }
                else
                {
                    avg += (d - deemph_a / 2) / deemph_a;
                }

                lp[i] = Convert.ToInt16(avg);
            }
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
                for (var i = 0; i < length; i++)
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

        public int LowPass(short[] iqData, int count, double samplerate)
        {
            var downsample = Convert.ToInt32((1000000 / samplerate) + 1);

            var i = 0;
            var i2 = 0;
            while (i < count - 1)
            {
                now_r += iqData[i + 0];
                now_j += iqData[i + 1];
                i += 2;
                prev_index++;
                if (prev_index < downsample)
                {
                    continue;
                }
                iqData[i2] = now_r;
                iqData[i2 + 1] = now_j;
                prev_index = 0;
                now_r = 0;
                now_j = 0;
                i2 += 2;
            }

            return i2;
        }

        public int LowPassWithMoveParallel(byte[] iqData, short[] res, int count, double samplerate, short moveVector)
        {
            int downsample = Convert.ToInt32((1000000 / samplerate) + 1);
            int adjustedCount = count - 1;

            var finalCount = 0;

            Parallel.For(0, adjustedCount / (2 * downsample), (index) =>
            {
                int i = index * 2 * downsample;
                short now_j = 0;
                short now_r = 0;

                for (int j = 0; j < downsample; j++, i += 2)
                {
                    now_r += (short)(iqData[i] + moveVector);
                    now_j += (short)(iqData[i + 1] + moveVector);
                }

                res[index * 2] = now_r;
                res[index * 2 + 1] = now_j;

                finalCount = index * 2 + 1;
            });

            return finalCount;
        }

        public int LowPassWithMoveOpt(byte[] iqData, short[] res, int count, double samplerate, short moveVector)
        {
            int downsample = Convert.ToInt32((1000000 / samplerate) + 1);
            int adjustedCount = count - 1;
            int i = 0, i2 = 0;
            short nr = now_r, nj = now_j;
            int pi = prev_index;

            // Optimalizace: Předvýpočet limitu pro smyčku
            int loopLimit = adjustedCount - (adjustedCount % (2 * downsample));

            while (i < loopLimit)
            {
                for (int end = i + 2 * downsample; i < end; i += 2)
                {
                    nr += (short)(iqData[i] + moveVector);
                    nj += (short)(iqData[i + 1] + moveVector);
                }

                res[i2++] = nr;
                res[i2++] = nj;
                nr = 0;
                nj = 0;
            }

            // Zpracování zbývajících dat
            for (; i < adjustedCount; i += 2)
            {
                nr += (short)(iqData[i] + moveVector);
                nj += (short)(iqData[i + 1] + moveVector);
            }

            now_r = nr;
            now_j = nj;
            prev_index = pi;

            return i2;
        }

        public static short[] Move(byte[] iqData, int count, short vector)
        {
            var buff = new short[count];

            for (int i = 0; i < count; i++)
            {
                buff[i] = (short)(iqData[i] + vector);
            }

            return buff;
        }

    }
}