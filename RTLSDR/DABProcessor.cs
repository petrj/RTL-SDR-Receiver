using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using LoggerService;

namespace RTLSDR
{
    public class DABProcessor
    {
        private const int INPUT_RATE = 2048000;
        private const int BANDWIDTH = 1536000;
        private bool _synced = false;

        private object _lock = new object();

        private Queue<Complex> _samplesQueue = new Queue<Complex>();
        private BackgroundWorker _OFDMWorker = null;

        private const int T_F = 196608;
        private const int T_null = 2656;
        private const int T_u = 2048;

        private double _sLevel = 0;
        private int localPhase = 0;

        private short fineCorrector = 0;
        private int coarseCorrector = 0;

        private ILoggingService _loggingService;

        public DABProcessor(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            BuildOscillatorTable();

            _OFDMWorker = new BackgroundWorker();
            _OFDMWorker.WorkerSupportsCancellation = true;
            _OFDMWorker.DoWork += _OFDMWorker_DoWork;
            _OFDMWorker.RunWorkerAsync();
        }

        private Complex GetSample(int phase, int msTimeOut = 1000)
        {
            var samples = GetSamples(1, phase, msTimeOut);
            if (samples == null)
                throw new Exception("No samples");

            return samples[0];
        }

        private Complex[] GetSamples(int count, int phase, int msTimeOut = 1000)
        {
            var samplesFound = false;

            var getStart = DateTime.Now;

            while (!samplesFound)
            {
                lock (_lock)
                {
                    if (_samplesQueue.Count >= count)
                    {
                        var res = new Complex[count];
                        for (var i = 0; i < count; i++)
                        {
                            var sample = res[i] = _samplesQueue.Dequeue();

                            localPhase -= phase;
                            localPhase = (localPhase + INPUT_RATE) % INPUT_RATE;
                            sample = sample.Multiply(OscillatorTable[localPhase]);
                            _sLevel = 0.00001F * sample.L1Norm() + (1.0F - 0.00001F) * _sLevel;

                            //_loggingService.Info($"sLevel: {_sLevel}");
                        }
                        return res;
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

        private void Sync()
        {
            var syncBufferSize = 32768;
            var envBuffer = new double[syncBufferSize];
            double currentStrength = 0;
            var syncBufferIndex = 0;
            var syncBufferMask = syncBufferSize - 1;

            // process first T_F/2 samples  (see void OFDMProcessor::run())
            var samples = GetSamples(T_F / 2, 0);

            while (!_synced)
            {
                var next50Samples = GetSamples(50, 0);
                for (var i = 0; i < 50; i++)
                {
                    var sample = next50Samples[i];
                    envBuffer[syncBufferIndex] = sample.L1Norm();
                    currentStrength += envBuffer[syncBufferIndex];
                    syncBufferIndex++;

                    //_loggingService.Info($"# {i} r: {sample.Real} i:{sample.Imaginary}");
                }

                // looking for the null level

                var counter = 0;
                var ok = true;
                while (currentStrength / 50 > 0.5F * _sLevel)
                {
                    var sample = GetSample(coarseCorrector + fineCorrector);
                    envBuffer[syncBufferIndex] = sample.L1Norm();
                    //  update the levels
                    currentStrength += envBuffer[syncBufferIndex] - envBuffer[(syncBufferIndex - 50) & syncBufferMask];
                    syncBufferIndex = (syncBufferIndex + 1) & syncBufferMask;
                    counter++;
                    if (counter > T_F)
                    {
                        // not synced!
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                {
                    continue;
                }

                // looking for the end of the null period.

                counter = 0;
                ok = true;
                while (currentStrength / 50 < 0.75F * _sLevel)
                {
                    var sample = GetSample(coarseCorrector + fineCorrector);
                    envBuffer[syncBufferIndex] = sample.L1Norm();
                    //  update the levels
                    currentStrength += envBuffer[syncBufferIndex] - envBuffer[(syncBufferIndex - 50) & syncBufferMask];
                    syncBufferIndex = (syncBufferIndex + 1) & syncBufferMask;
                    counter++;
                    if (counter > T_null + 50)
                    {
                        // not synced!
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                {
                    continue;
                } else
                {
                    _synced = true;
                }
            }

            // find first sample

            samples = GetSamples(T_u, coarseCorrector + fineCorrector);

            var startIndex = FindIndex(samples);

            _synced = true;
        }

        private int FindIndex(Complex[] samples)
        {
            return - 1;
        }

        private void _OFDMWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_OFDMWorker.CancellationPending)
            {
                if (!_synced)
                {
                    Sync();
                } else
                {
                    GetSamples(T_u, 0);
                }
            }
        }

        public Complex[] OscillatorTable { get; set; } = null;

        public static Complex[] ToDSPComplex(byte[] iqData, int length)
        {
            var res = new Complex[length];

            for (int i = 0; i < length/2; i++)
            {
                res[i] = new Complex(
                    (iqData[i * 2 + 0] - 128) / 128.0,
                    (iqData[i * 2 + 1] - 128) / 128.0);
            }

            return res;
        }

        private void BuildOscillatorTable()
        {
            OscillatorTable = new Complex[INPUT_RATE];

            for (int i = 0; i < INPUT_RATE; i++)
            {
                OscillatorTable[i] = new Complex(
                    Math.Cos(2.0 * Math.PI * i / INPUT_RATE),
                    Math.Sin(2.0 * Math.PI * i / INPUT_RATE));
            }

        }

        public void AddSamples(byte[] IQData, int length)
        {
            Console.WriteLine($"Adding {length} samples");

            lock (_lock)
            {
                foreach (var item in ToDSPComplex(IQData, length))
                {
                    _samplesQueue.Enqueue(item);
                }
            }
        }
    }
}
