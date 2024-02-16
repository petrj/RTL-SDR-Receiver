using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LoggerService;
using RTLSDR.Core;

namespace RTLSDR.DAB
{
    /*
        Free .NET DAB+ library

        -   based upon welle.io (https://github.com/AlbrechtL/welle.io)
        -   DAB documentation: https://www.etsi.org/deliver/etsi_en/300400_300499/300401/02.01.01_60/en_300401v020101p.pdf
    */

    public class DABProcessor : IDemodulator
    {
        public int Samplerate { get; set; } = 2048000; // INPUT_RATE

        private bool _finish = false;

        private const int BANDWIDTH = 1536000;
        private const int SEARCH_RANGE = 2 * 36;
        private const int CORRELATION_LENGTH = 24;
        private const int CUSize = 4 * 16;

        private ConcurrentQueue<FComplex[]> _samplesQueue = new ConcurrentQueue<FComplex[]>();
        private ConcurrentQueue<List<FComplex[]>> _processDataQueue = new ConcurrentQueue<List<FComplex[]>>();

        private FComplex[] _currentSamples = null;
        private int _currentSamplesPosition = -1;

        private FrequencyInterleaver _interleaver;

        private BackgroundWorker _OFDMWorker = null;
        private BackgroundWorker _processDataWorker = null;

        private DateTime _lastQueueSizeNotifyTime = DateTime.MinValue;
        private DateTime _startTime = DateTime.MinValue;

        private double _findFirstSymbolTotalTime = 0;
        private double _syncTime = 0;
        private double _getAllSymbolsTime = 0;
        private double _processDataTime = 0;
        private double _coarseCorrectorTime = 0;
        private double _getNULLSymbolsTime = 0;

        private const int T_F = 196608;
        private const int T_null = 2656;
        private const int T_u = 2048;
        private const int L = 76;
        private const int T_s = 2552;
        private const int K = 1536;
        private const int carrierDiff = 1000;

        private const int BitRate = 120;

        // DAB mode I:
        private const int DABModeINumberOfNlocksPerCIF = 18;

        private double _sLevel = 0;
        private int localPhase = 0;

        private short _fineCorrector = 0;
        private int _coarseCorrector = 0;

        private ILoggingService _loggingService;
        private FComplex[] _oscillatorTable { get; set; } = null;
        private double[] _refArg;
        private FourierSinCosTable _sinCosTable = null;

        private PhaseTable _phaseTable = null;
        private FICData _fic;

        public bool CoarseCorrector { get; set; } = true;

        public DABSubChannel ProcessingSubChannel { get; set; } = null;

        private sbyte[] InterleaveMap = new sbyte[16] { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };
        private int _countforInterleaver = 0;

        private EEPProtection _EEPProtection;
        private Viterbi _FICViterbi;
        private Viterbi _MSCViterbi;
        private EnergyDispersal _energyDispersal;

        public event EventHandler OnDemodulated;
        public event EventHandler OnFinished;
        public delegate void OnDemodulatedEventHandler(object sender, DataDemodulatedEventArgs e);
        public delegate void OnFinishedEventHandler(object sender, EventArgs e);

        public DABProcessor(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            BuildOscillatorTable();

            _sinCosTable = new FourierSinCosTable()
            {
                Count = T_u
            };

            _OFDMWorker = new BackgroundWorker();
            _OFDMWorker.WorkerSupportsCancellation = true;
            _OFDMWorker.DoWork += _OFDMWorker_DoWork;
            _OFDMWorker.RunWorkerAsync();

            _processDataWorker = new BackgroundWorker();
            _processDataWorker.WorkerSupportsCancellation = true;
            _processDataWorker.DoWork += _processDataWorker_DoWork;
            _processDataWorker.RunWorkerAsync();

            _interleaver = new FrequencyInterleaver(T_u, K);
            //_constellationPoints = new List<Complex>();

            _phaseTable = new PhaseTable(_loggingService, Samplerate, T_u);

            _refArg = new double[CORRELATION_LENGTH];

            for (int i = 0; i < CORRELATION_LENGTH; i++)
            {
                _refArg[i] = FComplex.Multiply(_phaseTable.RefTable[(T_u + i) % T_u], _phaseTable.RefTable[(T_u + i + 1) % T_u].Conjugated()).PhaseAngle();
            }

            _FICViterbi = new Viterbi(768);
            _MSCViterbi = new Viterbi(2880);

            _fic = new FICData(_loggingService, _FICViterbi);

            _energyDispersal = new EnergyDispersal();
        }

        public double PercentSignalPower
        {
            get
            {
                return 0;
            }
        }

        public void Finish()
        {
            _finish = true;
        }

        public FICData FIC
        {
            get
            {
                return _fic;
            }
        }

        private FComplex GetSample(int phase, int msTimeOut = 1000)
        {
            var samples = GetSamples(1, phase, msTimeOut);
            if (samples == null)
                throw new NoSamplesException();

            return samples[0];
        }

        private FComplex[] GetSamples(int count, int phase, int msTimeOut = 1000)
        {
            var getStart = DateTime.Now;
            var res = new FComplex[count];
            float rr;
            float ri;
            FComplex ot;

            int i = 0;
            while (i < count)
            {
                if (_currentSamples == null || _currentSamplesPosition >= _currentSamples.Length)
                {
                    var ok = _samplesQueue.TryDequeue(out _currentSamples);

                    if (!ok)
                    {
                        var span = DateTime.Now - getStart;
                        if (span.TotalMilliseconds > msTimeOut)
                        {
                            throw new NoSamplesException();
                        } else
                        {
                            Thread.Sleep(300);
                        }

                        continue;
                    } else
                    {
                        _currentSamplesPosition = 0;
                    }
                }
                res[i] = _currentSamples[_currentSamplesPosition];

                localPhase -= phase;
                localPhase = (localPhase + Samplerate) % Samplerate;

                res[i] = FComplex.Multiply(res[i], _oscillatorTable[localPhase]);
                //speed optimalization:
                //rr = res[i].Real;
                //ri = res[i].Imaginary;
                //ot = _oscillatorTable[localPhase];
                //res[i].Real = (rr * ot.Real - ri * ot.Imaginary);
                //res[i].Imaginary = (rr * ot.Imaginary + ri * ot.Real);

                _sLevel = 0.00001F * res[i].L1Norm() + (1.0F - 0.00001F) * _sLevel;
                //speed optimalization:
                //_sLevel = 0.00001F * (Math.Abs(res[i].Real) + Math.Abs(res[i].Imaginary)) + (1.0F - 0.00001F) * _sLevel;

                i++;
                _currentSamplesPosition++;
            }

            if ((DateTime.Now - _lastQueueSizeNotifyTime).TotalSeconds > 5)
            {
                _lastQueueSizeNotifyTime = DateTime.Now;
                Stat();
            }

            return res;
        }

        public void Stat()
        {
            _loggingService.Info($"<-------------------------------------------------------------- Samples queue size: {(_samplesQueue.Count).ToString("N0")} batches");
            _loggingService.Info($"                                                                Data    queue size: {(_processDataQueue.Count).ToString("N0")} batches");
            _lastQueueSizeNotifyTime = DateTime.Now;
            _loggingService.Debug($"-[]-Find first symbol time: {_findFirstSymbolTotalTime.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"-[]-Sync time:              {_syncTime.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"-[]-Coarse corrector Time:  {_coarseCorrectorTime.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"-[]-Get all symbols time:   {_getAllSymbolsTime.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"-[]-Process data time:      {_processDataTime.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"-[]-Get NULL symbols time:  {_getNULLSymbolsTime.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"      FFT time:  {Fourier.TotalFFTTimeMs.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"      DFT time:  {Fourier.TotalDFTTimeMs.ToString("N2").PadLeft(10, ' ')} ms");
            _loggingService.Debug($"-------------Total time:    { (DateTime.Now - _startTime).ToString().PadLeft(10, ' ')}");
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

            var totalContinuedCount = 0;

            // process first T_F/2 samples  (see void OFDMProcessor::run())
            var samples = GetSamples(T_F / 2, 0);
            samples = null;

            var synced = false;
            while (!synced)
            {
                syncBufferIndex = 0;
                currentStrength = 0;

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
                    currentStrength += envBuffer[syncBufferIndex] - envBuffer[syncBufferIndex - 50 & syncBufferMask];
                    syncBufferIndex = syncBufferIndex + 1 & syncBufferMask;
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
                    totalContinuedCount++;
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
                    currentStrength += envBuffer[syncBufferIndex] - envBuffer[syncBufferIndex - 50 & syncBufferMask];
                    syncBufferIndex = syncBufferIndex + 1 & syncBufferMask;
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
                    totalContinuedCount++;
                    continue;
                }
                else
                {
                    synced = true;
                }
            }

            if (totalContinuedCount>2)
            {
                _loggingService.Debug($"totalContinuedCount: {totalContinuedCount++}");
            }

            return synced;
        }

        private int FindIndex(FComplex[] rawSamples)
        {
            try
            {
                // rawSamples must remain intact to CoarseCorrector
                var samples = FComplex.CloneComplexArray(rawSamples);

                Fourier.FFTBackward(samples);

                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = FComplex.Multiply(samples[i], _phaseTable.RefTable[i].Conjugated());
                }

                samples = Fourier.DFTBackward(samples, _sinCosTable.CosTable, _sinCosTable.SinTable);

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
                    if (peak.Value > thresh)
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

                    if (peak.Index < earliestPeak.Index)
                    {
                        earliestPeak = peak;
                    }
                }

                return earliestPeak.Index;

            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, "Error finding index");
                return -1;
            }
        }

        private void _OFDMWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"OFDMWorker starting");

            _startTime = DateTime.Now;
            bool synced = false;
            try
            {
                while (!_OFDMWorker.CancellationPending)
                {
                    try
                    {

                        if (!synced)
                        {
                            var startSyncTime = DateTime.Now;
                            synced = Sync();
                            _syncTime += (DateTime.Now - startSyncTime).TotalMilliseconds;

                            if (!synced)
                            {
                                _loggingService.Debug($"-[]-Sync failed!");
                                continue;
                            }
                        }

                        // find first sample

                        var startFirstSymbolSearchTime = DateTime.Now;

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

                        _findFirstSymbolTotalTime += (DateTime.Now - startFirstSymbolSearchTime).TotalMilliseconds;


                        var startCoarseCorrectorTime = DateTime.Now;

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

                        _coarseCorrectorTime += (DateTime.Now - startCoarseCorrectorTime).TotalMilliseconds;

                        var startGetAllSymbolsTime = DateTime.Now;

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
                        _getAllSymbolsTime += (DateTime.Now - startGetAllSymbolsTime).TotalMilliseconds;

                        _processDataQueue.Enqueue(allSymbols);

                        var startGetNULLSymbolsTime = DateTime.Now;

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

                        _getNULLSymbolsTime += (DateTime.Now - startGetNULLSymbolsTime).TotalMilliseconds;

                    }
                    catch (NoSamplesException)
                    {
                        if (_finish)
                        {
                            OnFinished(this, new EventArgs());
                            _finish = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"-[]-OFDMWorker finished, total time: {(DateTime.Now - _startTime).TotalMinutes.ToString("N2").PadLeft(10, ' ')} min");
        }

        private void _processDataWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"_processDataWorker starting");

            try
            {
                while (!_processDataWorker.CancellationPending)
                {
                    List<FComplex[]> allSymbols;

                    var ok = _processDataQueue.TryDequeue(out allSymbols);

                    if (!ok)
                    {
                        Thread.Sleep(300);
                    }
                    else
                    {
                        var startProcessDataTime = DateTime.Now;

                        if (ProcessingSubChannel != null)
                        {
                            if (_EEPProtection == null)
                            {
                                _EEPProtection = new EEPProtection(ProcessingSubChannel.Bitrate, EEPProtectionProfile.EEP_A, ProcessingSubChannel.ProtectionLevel, _MSCViterbi);
                            }

                            ProcessData(allSymbols);
                        }

                        _processDataTime += (DateTime.Now - startProcessDataTime).TotalMilliseconds;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"_processDataWorker finished");
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
                var mscData = new List<sbyte>();

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

                        real = real > 0 ? Math.Floor(real) : Math.Floor(real) + 1;
                        imag = imag > 0 ? Math.Floor(imag) : Math.Floor(imag) + 1;

                        iBits[i] = Convert.ToSByte(real);
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
                    }
                    else
                    {
                        mscData.AddRange(iBits);
                    }
                }

                ProcessMSCData(mscData.ToArray());
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        private void ProcessMSCData(sbyte[] MSCData)
        {
            if (ProcessingSubChannel == null)
                return;

            var startPos = Convert.ToInt32(ProcessingSubChannel.StartAddr * CUSize);
            var count = Convert.ToInt32(ProcessingSubChannel.Length * CUSize);

            var DABBuffer = new sbyte[count];
            Buffer.BlockCopy(MSCData, startPos, DABBuffer, 0, count);

            // deinterleave

            var tempX = new sbyte[count];

            var interleaverIndex = 0;
            var interleaveData = new sbyte[16, count];

            do
            {
                for (var i = 0; i < count; i++)
                {
                    var index = (interleaverIndex + InterleaveMap[i % 16]) % 16;
                    tempX[i] = interleaveData[index, i];
                    interleaveData[interleaverIndex, i] = MSCData[i];
                }
                interleaverIndex = (interleaverIndex + 1) % 16;

                _countforInterleaver++;
            } while (_countforInterleaver <= 16);

            var bytes = _EEPProtection.Deconvolve(DABBuffer);
            var outV = _energyDispersal.Dedisperse(bytes);
            var finalBytes = GetFrameBytes(outV);

            if (OnDemodulated != null)
            {
                var arg = new DataDemodulatedEventArgs();
                arg.Data = finalBytes;

                OnDemodulated(this, arg);
            }
        }

        /// <summary>
        /// Convert 8 bits (stored in one uint8) into one uint8
        /// </summary>
        /// <returns></returns>
        private byte[] GetFrameBytes(byte[] v)
        {
            try
            {

                var length = 24 * BitRate / 8; // should be 2880 bytes

                var res = new byte[length];

                for (var i = 0; i < length; i++)
                {
                    res[i] = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        res[i] <<= 1;
                        res[i] |= Convert.ToByte(v[8 * i + j] & 01);
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                return null;
            }
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

            noise /= low - 90 + 100;
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
            var res = new FComplex[length / 2];

            float factor = 1.0f / 128.0f;

            for (int i = 0; i < length / 2; i++)
            {
                res[i] = new FComplex(
                                (iqData[i * 2] - 128) * factor,
                                (iqData[i * 2 + 1] - 128) * factor
                            );
            }

            return res;
        }

        private void BuildOscillatorTable()
        {
            _oscillatorTable = new FComplex[Samplerate];

            for (int i = 0; i < Samplerate; i++)
            {
                _oscillatorTable[i] = new FComplex(
                    Math.Cos(2.0 * Math.PI * i / Samplerate),
                    Math.Sin(2.0 * Math.PI * i / Samplerate));
            }
        }

        public void AddSamples(byte[] IQData, int length)
        {
            //Console.WriteLine($"Adding {length} samples");

            var dspComplexArray = ToDSPComplex(IQData, length);
            _samplesQueue.Enqueue(dspComplexArray);
        }
    }
}
