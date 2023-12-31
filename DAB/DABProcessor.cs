﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LoggerService;

namespace DAB
{
    /*
        Free .NET DAB+ library

        -   based upon welle.io (https://github.com/AlbrechtL/welle.io)
        -   DAB documentation: https://www.etsi.org/deliver/etsi_en/300400_300499/300401/02.01.01_60/en_300401v020101p.pdf
    */

    public class DABProcessor
    {
        private const int INPUT_RATE = 2048000;
        private const int BANDWIDTH = 1536000;
        private const int SEARCH_RANGE = (2 * 36);
        private const int CORRELATION_LENGTH = 24;

        private object _lock = new object();

        private Queue<FComplex> _samplesQueue = new Queue<FComplex>();
        private FrequencyInterleaver _interleaver;
        private BackgroundWorker _OFDMWorker = null;

        private DateTime _lastQueueSizeNotifyTime = DateTime.MinValue;

        private const int T_F = 196608;
        private const int T_null = 2656;
        private const int T_u = 2048;
        private const int L = 76;
        private const int T_s = 2552;
        private const int K = 1536;
        private const int carrierDiff = 1000;

        // DAB mode I:
        private const int DABModeINumberOfNlocksPerCIF = 18;

        private double _sLevel = 0;
        private int localPhase = 0;

        private short _fineCorrector = 0;
        private int _coarseCorrector = 0;

        private ILoggingService _loggingService;
        private FComplex[] _oscillatorTable { get; set; } = null;
        private double[] _refArg;

        private PhaseTable _phaseTable = null;
        private FICData _fic;

        public bool CoarseCorrector { get; set; } = false;

        public DABProcessor(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            _fic = new FICData(_loggingService);

            BuildOscillatorTable();

            _OFDMWorker = new BackgroundWorker();
            _OFDMWorker.WorkerSupportsCancellation = true;
            _OFDMWorker.DoWork += _OFDMWorker_DoWork;
            _OFDMWorker.RunWorkerAsync();

            _interleaver = new FrequencyInterleaver(T_u, K);
            //_constellationPoints = new List<Complex>();

            _phaseTable = new PhaseTable(_loggingService, INPUT_RATE, T_u);

            _refArg = new double[CORRELATION_LENGTH];

            for (int i = 0; i < CORRELATION_LENGTH; i++)
            {
                _refArg[i] = FComplex.Multiply(_phaseTable.RefTable[(T_u + i) % T_u], _phaseTable.RefTable[(T_u + i + 1) % T_u].Conjugated()).PhaseAngle();
            }
        }

        private FComplex GetSample(int phase, int msTimeOut = 1000)
        {
            var samples = GetSamples(1, phase, msTimeOut);
            if (samples == null)
                throw new Exception("No samples");

            return samples[0];
        }

        private FComplex[] GetSamples(int count, int phase, int msTimeOut = 1000)
        {
            var samplesFound = false;

            var getStart = DateTime.Now;

            while (!samplesFound)
            {
                lock (_lock)
                {
                    if (_samplesQueue.Count >= count)
                    {
                        var res = new FComplex[count];

                        for (var i = 0; i < count; i++)
                        {
                            res[i] = _samplesQueue.Dequeue();

                            localPhase -= phase;
                            localPhase = (localPhase + INPUT_RATE) % INPUT_RATE;
                            res[i] = FComplex.Multiply(res[i],_oscillatorTable[localPhase]);
                            _sLevel = 0.00001F * res[i].L1Norm() + (1.0F - 0.00001F) * _sLevel;

                            //_loggingService.Info($"sLevel: {_sLevel}");
                        }

                        if ((DateTime.Now - _lastQueueSizeNotifyTime).TotalSeconds > 5)
                        {
                            _loggingService.Info($"<-- Queue size: {(_samplesQueue.Count / 1024).ToString("N0")} KSamples");
                            _lastQueueSizeNotifyTime = DateTime.Now;
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
            samples = null;

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
                    var sample = GetSample(_coarseCorrector + _fineCorrector);
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
                    var sample = GetSample(_coarseCorrector + _fineCorrector);
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

        private int FindIndex(FComplex[] samples)
        {
            try
            {
                Fourier.FFTBackward(samples);

                for (var i =0; i < samples.Length; i++)
                {
                    samples[i] = FComplex.Multiply(samples[i], _phaseTable.RefTable[i].Conjugated());
                }

                // calling DFT leads to OutOfMemory!
                Fourier.DFTBackward(samples);

                var factor = 1.0 / samples.Length;

                //// scale all entries
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i].Multiply(factor);
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
                        var value = samples[i + j].Abs();
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
            try
            {

                while (!_OFDMWorker.CancellationPending)
                {
                    if (!synced)
                    {
                        var startSyncTime = DateTime.Now;
                        synced = Sync();
                        _loggingService.Debug($"-[]-Sync time: {(DateTime.Now - startSyncTime).TotalMilliseconds} ms");

                        if (!synced)
                            continue;
                    }

                    // find first sample

                    var samples = GetSamples(T_u, _coarseCorrector + _fineCorrector);

                    //var findIndexTime = DateTime.Now;
                    var startIndex = FindIndex(samples);
                    //_loggingService.Debug($"-[]-Find index time: {(DateTime.Now - findIndexTime).TotalMilliseconds} ms");

                    if (startIndex == -1)
                    {
                        // not synced
                        synced = false;
                        continue;
                    }

                    //var processDataTime = DateTime.Now;

                    var firstOFDMBuffer = new FComplex[T_u];
                    for (var i = 0; i < T_u - startIndex; i++)
                    {
                        firstOFDMBuffer[i] = samples[i + startIndex];
                    }

                    var missingSamples = GetSamples(startIndex, _coarseCorrector + _fineCorrector);
                    for (var i = T_u - startIndex; i < T_u; i++)
                    {
                        firstOFDMBuffer[i] = missingSamples[i - (T_u - startIndex)];
                    }

                    // coarse corrector
                    if (CoarseCorrector)
                    {
                        int correction = ProcessPRS(firstOFDMBuffer);
                        if (correction != 100)
                        {
                            _coarseCorrector += correction * carrierDiff;
                            if (Math.Abs(_coarseCorrector) > 35 * 1000)
                                _coarseCorrector = 0;
                        }
                    }

                    var allSymbols = new List<FComplex[]>();
                    allSymbols.Add(firstOFDMBuffer);

                    // ofdmBuffer.resize(params.L * params.T_s);

                    var FreqCorr = new FComplex(0, 0);

                    for (int sym = 1; sym < L; sym++)
                    {
                        var buf = GetSamples(T_s, _coarseCorrector + _fineCorrector);
                        allSymbols.Add(buf);

                        for (int i = T_u; i < T_s; i++)
                        {
                            FreqCorr.Add(FComplex.Multiply(buf[i], buf[i - T_u].Conjugated()));
                        }
                    }

                    ProcessData(allSymbols);

                    //_loggingService.Debug($"-[]-Process time: {(DateTime.Now - processDataTime).TotalMilliseconds} ms");

                    //var nullReadingTime = DateTime.Now;

                    _fineCorrector += Convert.ToInt16(0.1 * FreqCorr.PhaseAngle() / Math.PI * (carrierDiff / 2));

                    // save NULL data:

                    var nullSymbol = GetSamples(T_null, _coarseCorrector + _fineCorrector);

                    if (_fineCorrector > carrierDiff / 2)
                    {
                        _coarseCorrector += carrierDiff;
                        _fineCorrector -= carrierDiff;
                    }
                    else
                    if (_fineCorrector < -carrierDiff / 2)
                    {
                        _coarseCorrector -= carrierDiff;
                        _fineCorrector += carrierDiff;
                    }

                    //_loggingService.Debug($"-[]-Null read time: {(DateTime.Now - nullReadingTime).TotalMilliseconds} ms");
                }
            } catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        private int ProcessPRS(FComplex[] data)
        {
            var index = 100;
            var correlationVector = new double[SEARCH_RANGE + CORRELATION_LENGTH];

            var fft_buffer = FComplex.CloneComplexArray(data);
            Fourier.FFTBackward(fft_buffer);

            // FreqsyncMethod::CorrelatePRS:

            for (int i = 0; i < SEARCH_RANGE + CORRELATION_LENGTH; i++)
            {
                var baseIndex = T_u - SEARCH_RANGE / 2 + i;

                correlationVector[i] = FComplex.Multiply(fft_buffer[baseIndex % T_u], fft_buffer[(baseIndex + 1) % T_u].Conjugated()).PhaseAngle();
            }

            double MMax = 0;
            for (int i = 0; i < SEARCH_RANGE; i++)
            {
                double sum = 0;
                for (int j = 0; j < CORRELATION_LENGTH; j++)
                {
                    sum += Math.Abs(_refArg[j] * correlationVector[i + j]);
                    if (sum > MMax)
                    {
                        MMax = sum;
                        index = i;
                    }
                }
            }

            return T_u - SEARCH_RANGE / 2 + index - T_u;
        }

        private void ProcessData(List<FComplex[]> allSymbols)
        {
           try
           {
                // processPRS:
                var phaseReference = allSymbols[0];

                Fourier.FFTBackward(phaseReference);

                var snr = 0.0;
                snr = 0.7 * snr + 0.3 * get_snr(phaseReference);

                // decodeDataSymbol:

                var iBits = new sbyte[K * 2];

                for (var sym = 1; sym < allSymbols.Count; sym++)
                {
                    var T_g = T_s - T_u;
                    var croppedSymbols = new FComplex[T_u];
                    for (var c = 0; c < T_u; c++)
                    {
                        croppedSymbols[c] = allSymbols[sym][c + T_g];
                    }

                    Fourier.FFTBackward(croppedSymbols);

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
                        var r1 = FComplex.Multiply(croppedSymbols[index], phaseReference[index].Conjugated());
                        phaseReference[index] = croppedSymbols[index];

                        var ab1 = 127.0f / r1.L1Norm();
                        /// split the real and the imaginary part and scale it

                        var real = -r1.Real * ab1;
                        var imag = -r1.Imaginary * ab1;

                        real = (real > 0) ? Math.Floor(real) : Math.Floor(real) + 1;
                        imag = (imag > 0) ? Math.Floor(imag) : Math.Floor(imag) + 1;

                        iBits[i] = Convert.ToSByte( real);
                        iBits[K + i] = Convert.ToSByte(imag);

                        /*
                        if (i % constellationDecimation == 0)
                        {
                            constellationPoints.push_back(r1);
                        }
                        */
                    }

                    if (sym < 4)
                    {
                        _fic.Parse(iBits, sym);
                    } else
                    {
                        ProcessMSCData(iBits, sym);
                    }
                }
            } catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        private void ProcessMSCData(sbyte[] MSCData, int blkno)
        {
            var currentblk = (blkno - 4) % DABModeINumberOfNlocksPerCIF;

            if (currentblk < DABModeINumberOfNlocksPerCIF - 1)
                return;
        }

        private short get_snr(FComplex[] v)
        {
            int i;
            double noise = 0;
            double signal = 0;

            var low = T_u / 2 - K / 2;
            var high = low + K;

            for (i = 70; i < low - 20; i++) // low - 90 samples
                noise += v[(T_u / 2 + i) % T_u].Abs();

            for (i = high + 20; i < high + 120; i++) // 100 samples
                noise += v[(T_u / 2 + i) % T_u].Abs();

            noise /= (low - 90 + 100);
            for (i = T_u / 2 - K / 4; i < T_u / 2 + K / 4; i++)
                signal += v[(T_u / 2 + i) % T_u].Abs();

            var dB_signal_new = get_db_over_256(signal / (K / 2.0));
            var dB_noise_new = get_db_over_256(noise);
            var snr_new = dB_signal_new - dB_noise_new;

            return Convert.ToInt16(snr_new);
        }

        private static double get_db_over_256(double x)
        {
            return 20 * Math.Log10((x + 1.0f) / 256.0f);
        }

        public static FComplex[] ToDSPComplex(byte[] iqData, int length)
        {
            var res = new FComplex[length/2];

            for (int i = 0; i < length/2; i++)
            {
                res[i] = new FComplex(
                    (iqData[i * 2 + 0] - 128) / 128.0,
                    (iqData[i * 2 + 1] - 128) / 128.0);
            }

            return res;
        }

        private void BuildOscillatorTable()
        {
            _oscillatorTable = new FComplex[INPUT_RATE];

            for (int i = 0; i < INPUT_RATE; i++)
            {
                _oscillatorTable[i] = new FComplex(
                    Math.Cos(2.0 * Math.PI * i / INPUT_RATE),
                    Math.Sin(2.0 * Math.PI * i / INPUT_RATE));
            }
        }

        public void AddSamples(byte[] IQData, int length)
        {
            //Console.WriteLine($"Adding {length} samples");

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
