using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading;
using LoggerService;

namespace DAB
{
    public class DABProcessor
    {
        private const int INPUT_RATE = 2048000;
        private const int BANDWIDTH = 1536000;

        private object _lock = new object();

        private Queue<Complex> _samplesQueue = new Queue<Complex>();
        private BackgroundWorker _OFDMWorker = null;

        private const int T_F = 196608;
        private const int T_null = 2656;
        private const int T_u = 2048;
        private const int L = 76;
        private const int T_s = 2552;

        private double _sLevel = 0;
        private int localPhase = 0;

        private short fineCorrector = 0;
        private int coarseCorrector = 0;

        private ILoggingService _loggingService;

        public Complex[] OscillatorTable { get; set; } = null;

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

        /// <summary>
        /// Sync samples position
        /// </summary>
        /// <returns>sync position</returns>
        private DataSyncPosition Sync()
        {
            var res = new DataSyncPosition();

            var syncBufferSize = 32768;
            var envBuffer = new double[syncBufferSize];
            double currentStrength = 0;
            var syncBufferIndex = 0;
            var syncBufferMask = syncBufferSize - 1;

            // process first T_F/2 samples  (see void OFDMProcessor::run())
            var samples = GetSamples(T_F / 2, 0);

            var synced = false;
            while (!synced)
            {
                // TODO: add break when total samples read exceed some value

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
                    synced = true;
                }
            }

            // find first sample

            samples = GetSamples(T_u, coarseCorrector + fineCorrector);

            res.StartIndex = FindIndex(samples);

            if (res.StartIndex == -1)
            {
                return res; // not synced
            }

            res.FirstOFDMBuffer = new Complex[T_u];
            for (var i=0;i<T_u- res.StartIndex; i++)
            {
                res.FirstOFDMBuffer[i] = samples[i+ res.StartIndex];
            }

            var missingSamples = GetSamples(res.StartIndex, coarseCorrector + fineCorrector);
            for (var i = T_u- res.StartIndex; i < T_u; i++)
            {
                res.FirstOFDMBuffer[i] = missingSamples[i- (T_u - res.StartIndex)];
            }

            res.Synced = true;

            return res; 
        }

        private int FindIndex(Complex[] rawSamples)
        {
            try
            {
                var samples = new Complex[rawSamples.Length];
                for (var s=0;s<rawSamples.Length;s++)
                {
                    samples[s] = rawSamples[s].Clone();
                }

                Accord.Math.FourierTransform.FFT(samples, Accord.Math.FourierTransform.Direction.Backward);

                var phaseTable = new PhaseTable(_loggingService, INPUT_RATE, T_u);

                for (var i =0; i < samples.Length; i++)
                {
                    samples[i] = samples[i] * Complex.Conjugate(phaseTable.RefTable[i]);
                }

                Accord.Math.FourierTransform.DFT(samples, Accord.Math.FourierTransform.Direction.Backward);

                var factor = 1.0 / samples.Length;

                //// scale all entries
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= factor;
                }

                var impulseResponseBuffer = new List<double>();
                for (var impulseResponseBufferIter = 0; impulseResponseBufferIter < samples.Length; impulseResponseBufferIter++)
                {
                    impulseResponseBuffer.Add(0);
                }

                // FFTPlacementMethod::EarliestPeakWithBinning:

                var bin_size = 20;
                var num_bins_to_keep = 4;
                var bins = new List<Peak>();
                double mean = 0;

                for (var i = 0; i + bin_size < samples.Length; i += bin_size)
                {
                    var peak = new Peak();
                    for (var j = 0; j < bin_size; j++)
                    {
                        var value = Complex.Abs(samples[i + j]);
                        mean += value;
                        impulseResponseBuffer[i + j] = value;

                        if (value > peak.Value)
                        {
                            peak.Value = value;
                            peak.Index = i + j;
                        }
                    }
                    bins.Add(peak);
                }

                mean /= samples.Length;

                if (bins.Count < num_bins_to_keep)
                {
                    throw new Exception("Sync err, not enough bins");
                }

                // Sort bins by highest peak
                bins.Sort();

                // Keep only bins that are not too far from highest peak
                var peak_index = bins[0].Index;
                var max_subpeak_distance = 500;

                var peaksCloseToMax = new List<Peak>();
                foreach (var peak in bins)
                {
                    if (peak.Index - peak_index < max_subpeak_distance)
                    {
                        peaksCloseToMax.Add(peak);

                        if (peaksCloseToMax.Count >= num_bins_to_keep)
                        {
                            break;
                        }
                    }
                }

                var thresh = 3.0 * mean;
                var peaksAboveTresh = new List<Peak>();
                foreach (var peak in peaksCloseToMax)
                {
                    if (peak.Value>thresh)
                    {
                        peaksAboveTresh.Add(peak);
                    }
                }

                if (peaksAboveTresh.Count == 0)
                    return -1;

                // earliest_bin

                Peak earliestPeak = peaksAboveTresh[0];
                foreach (var peak in peaksAboveTresh)
                {
                    if (peak == peaksAboveTresh[0])
                        continue;

                    if (peak.Index<earliestPeak.Index)
                    {
                        earliestPeak = peak;
                    }
                }

                return earliestPeak.Index;

            } catch(Exception ex)
            {
                _loggingService.Error(ex, "Error finding index");
                return -1;
            }
            return - 1;
        }

        private void _OFDMWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_OFDMWorker.CancellationPending)
            {
                var syncRes = Sync();

                var allSymbols = new List<Complex[]>();
                allSymbols.Add(syncRes.FirstOFDMBuffer);

                // ofdmBuffer.resize(params.L * params.T_s); 

                var FreqCorr = new Complex(0, 0);

                for (int sym = 1; sym < L; sym++)
                {
                    var buf = GetSamples(T_s, coarseCorrector + fineCorrector);
                    allSymbols.Add(buf);

                    for (int i = T_u; i < T_s; i++)
                    {
                        FreqCorr += buf[i] * Complex.Conjugate(buf[i - T_u]);
                    }
                }

                var x = 0;
            }
        }

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
