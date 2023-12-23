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
        private FrequencyInterleaver _interleaver;
        private BackgroundWorker _OFDMWorker = null;
        private List<Complex> _constellationPoints;

        private const int T_F = 196608;
        private const int T_null = 2656;
        private const int T_u = 2048;
        private const int L = 76;
        private const int T_s = 2552;
        private const int K = 1536;
        private const int carrierDiff = 1000;
        private const int constellationDecimation = 96;

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

            _interleaver = new FrequencyInterleaver(T_u, K);
            //_constellationPoints = new List<Complex>();
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
                            res[i] = _samplesQueue.Dequeue();

                            localPhase -= phase;
                            localPhase = (localPhase + INPUT_RATE) % INPUT_RATE;
                            res[i] = res[i].Multiply(OscillatorTable[localPhase]);
                            _sLevel = 0.00001F * res[i].L1Norm() + (1.0F - 0.00001F) * _sLevel;

                            //_loggingService.Info($"sLevel: {_sLevel}");
                        }
                        return res;
                    } else
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

        /// <summary>
        /// Sync samples position
        /// </summary>
        /// <returns>sync position</returns>
        private bool Sync()
        {
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

            return synced;
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

                for (var i = 0; i + bin_size < T_u; i += bin_size)
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
                    if (Math.Abs(peak.Index - peak_index) < max_subpeak_distance)
                    {
                        peaksCloseToMax.Add(peak);

                        if (peaksCloseToMax.Count>=num_bins_to_keep)
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
        }

        private void _OFDMWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool synced = false;
            while (!_OFDMWorker.CancellationPending)
            {
                if (!synced)
                {
                    synced = Sync();

                    if (!synced)
                        continue;
                }

                // find first sample

                var samples = GetSamples(T_u, coarseCorrector + fineCorrector);

                var startIndex = FindIndex(samples);

                if (startIndex == -1)
                {
                    // not synced
                    synced = false;
                    continue; 
                }

                var firstOFDMBuffer = new Complex[T_u];
                for (var i = 0; i < T_u - startIndex; i++)
                {
                    firstOFDMBuffer[i] = samples[i + startIndex];
                }

                var missingSamples = GetSamples(startIndex, coarseCorrector + fineCorrector);
                for (var i = T_u - startIndex; i < T_u; i++)
                {
                    firstOFDMBuffer[i] = missingSamples[i - (T_u - startIndex)];
                }

                var allSymbols = new List<Complex[]>();
                allSymbols.Add(firstOFDMBuffer);

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

                ProcessData(allSymbols);

                fineCorrector += Convert.ToInt16(0.1 * FreqCorr.Phase / Math.PI * (carrierDiff / 2));

                // save NULL data:

                var nullSymbol = GetSamples(T_null, coarseCorrector + fineCorrector);

                if (fineCorrector > carrierDiff / 2)
                {
                    coarseCorrector += carrierDiff;
                    fineCorrector -= carrierDiff;
                }
                else
                if (fineCorrector < -carrierDiff / 2)
                {
                    coarseCorrector -= carrierDiff;
                    fineCorrector += carrierDiff;
                }
            }
        }

        private void ProcessData(List<Complex[]> allSymbols)
        {
            try
            {

                // processPRS:
                var phaseReference = allSymbols[0];

                Accord.Math.FourierTransform.FFT(phaseReference, Accord.Math.FourierTransform.Direction.Backward);

                var snr = 0.0;
                snr = 0.7 * snr + 0.3 * get_snr(phaseReference);

                // decodeDataSymbol:

                var iBits = new byte[K * 2];

                for (var sym = 1; sym < allSymbols.Count; sym++)
                {
                    var T_g = T_s - T_u;
                    var croppedSymbols = new Complex[T_u];
                    for (var c = 0; c < T_u; c++)
                    {
                        croppedSymbols[c] = allSymbols[sym][c + T_g];
                    }

                    Accord.Math.FourierTransform.FFT(croppedSymbols, Accord.Math.FourierTransform.Direction.Backward);

                    for (var i = 0; i < K; i++)
                    {
                        var index = _interleaver.MapIn(i);

                        if (index < 0)
                        {
                            index += T_u;
                        }
                        /*
                         * decoding is computing the phase difference between
                         * carriers with the same index in subsequent symbols.
                         * The carrier of a symbols is the reference for the carrier
                         * on the same position in the next symbols
                         */
                        var r1 = croppedSymbols[index] * Complex.Conjugate(phaseReference[index]);
                        phaseReference[index] = croppedSymbols[index];

                        var ab1 = 127.0f / r1.L1Norm();
                        /// split the real and the imaginary part and scale it
   
                        var real = Math.Floor(-r1.Real * ab1);
                        var imag = Math.Floor(-r1.Imaginary * ab1);

                        iBits[i] = Convert.ToByte( real < 0 ? 255 + real : real );
                        iBits[K + i] = Convert.ToByte(imag < 0 ? 255 + imag : imag);

                        /*
                        if (i % constellationDecimation == 0)
                        {
                            constellationPoints.push_back(r1);
                        }
                        */
                    }

                    if (sym < 4)
                    {
                        // TODO: process FIC block from iBits[]
                    } else
                    {
                        // TODO: process MSC block from iBits[]
                    }
                }
            } catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        private short get_snr(Complex[] v)
        {
            int i;
            double noise = 0;
            double signal = 0;

            var low = T_u / 2 - K / 2;
            var high = low + K;

            for (i = 70; i < low - 20; i++) // low - 90 samples
                noise += Complex.Abs(v[(T_u / 2 + i) % T_u]);

            for (i = high + 20; i < high + 120; i++) // 100 samples
                noise += Complex.Abs(v[(T_u / 2 + i) % T_u]);

            noise /= (low - 90 + 100);
            for (i = T_u / 2 - K / 4; i < T_u / 2 + K / 4; i++)
                signal += Complex.Abs(v[(T_u / 2 + i) % T_u]);

            var dB_signal_new = get_db_over_256(signal / (K / 2.0));
            var dB_noise_new = get_db_over_256(noise);
            var snr_new = dB_signal_new - dB_noise_new;

            return Convert.ToInt16(snr_new);
        }

        private static double get_db_over_256(double x)
        {
            return 20 * Math.Log10((x + 1.0f) / 256.0f);
        }

        public static Complex[] ToDSPComplex(byte[] iqData, int length)
        {
            var res = new Complex[length/2];

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
