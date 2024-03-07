using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
        private DABState _state = new DABState();

        private bool _finish = false;

        private const int MinThreadNoDataMSDelay = 25;

        private const int BANDWIDTH = 1536000;
        private const int SEARCH_RANGE = 2 * 36;
        private const int CORRELATION_LENGTH = 24;
        private const int CUSize = 4 * 16; // 64

        private const int T_F = 196608;
        private const int T_null = 2656;
        private const int T_u = 2048;
        private const int L = 76;
        private const int T_s = 2552;
        private const int K = 1536;
        private const int CarrierDiff = 1000;
        private const int BitsperBlock = 2 * K; // 3072

        private ConcurrentQueue<FComplex[]> _samplesQueue = new ConcurrentQueue<FComplex[]>();
        private ConcurrentQueue<List<FComplex[]>> _OFDMDataQueue = new ConcurrentQueue<List<FComplex[]>>();
        private ConcurrentQueue<FICQueueItem> _ficDataQueue = new ConcurrentQueue<FICQueueItem>();
        private ConcurrentQueue<sbyte[]> _MSCDataQueue = new ConcurrentQueue<sbyte[]>();
        private ConcurrentQueue<byte[]> _DABQueue = new ConcurrentQueue<byte[]>();

        private FComplex[] _currentSamples = null;
        private int _currentSamplesPosition = -1;
        private long _totalSamplesRead = 0;
        private int _totalContinuedCount = 0;

        private const int SyncBufferSize = 32768;
        private float[] _syncEnvBuffer = new float[SyncBufferSize];
        private int _syncBufferMask = SyncBufferSize - 1;

        private FrequencyInterleaver _interleaver;

        private BackgroundWorker _SyncWorker = null;
        private BackgroundWorker _OFDMWorker = null;
        private BackgroundWorker _FICParserWorker = null;
        private BackgroundWorker _MSCDataParserWorker = null;
        private BackgroundWorker _DABWorker = null;
        private BackgroundWorker _statusWorker = null;

        private DateTime _startTime;
        private double _findFirstSymbolTotalTime = 0;
        private double _getFirstSymbolDataTotalTime = 0;
        private double _syncTime = 0;
        private double _SyncWorkerTime = 0;
        private double _getAllSymbolsTime = 0;
        private double _OFDMDataTime = 0;
        private double _coarseCorrectorTime = 0;
        private double _getNULLSymbolsTime = 0;
        private double _FICTime = 0;
        private double _MSCTime = 0;
        private double _DABTime = 0;

        // DAB mode I:
        private const int DABModeINumberOfBlocksPerCIF = 18;

        private int _cycles = 0;

        private float _sLevel = 0;
        private int _localPhase = 0;

        private short _fineCorrector = 0;
        private int _coarseCorrector = 0;

        private ILoggingService _loggingService;
        private FComplex[] _oscillatorTable { get; set; } = null;
        private double[] _refArg;
        private FourierSinCosTable _sinCosTable = null;

        private PhaseTable _phaseTable = null;
        private FICData _fic;

        private Viterbi _FICViterbi;

        private DABDecoder _DABDecoder = null;

        public int Samplerate { get; set; } = 2048000; // INPUT_RATE
        public bool CoarseCorrector { get; set; } = true;
        public DABSubChannel ProcessingSubChannel { get; set; } = null;

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

            _interleaver = new FrequencyInterleaver(T_u, K);
            //_constellationPoints = new List<Complex>();

            _phaseTable = new PhaseTable(_loggingService, Samplerate, T_u);

            _refArg = new double[CORRELATION_LENGTH];

            for (int i = 0; i < CORRELATION_LENGTH; i++)
            {
                _refArg[i] = FComplex.Multiply(_phaseTable.RefTable[(T_u + i) % T_u], _phaseTable.RefTable[(T_u + i + 1) % T_u].Conjugated()).PhaseAngle();
            }

            _FICViterbi = new Viterbi(768);

            _fic = new FICData(_loggingService, _FICViterbi);

            _startTime = DateTime.Now;

            _SyncWorker = StartBackgroundThread(_SyncWorker_DoWork);
            _OFDMWorker = StartBackgroundThread(_OFDMWorker_DoWork);
            _FICParserWorker = StartBackgroundThread(_FICParserWorker_DoWork);
            _MSCDataParserWorker = StartBackgroundThread(_MSCDataParserWorker_DoWork);
            _DABWorker = StartBackgroundThread(_DABWorker_DoWork);
            _statusWorker = StartBackgroundThread(_statusWorker_DoWork);
        }

        private BackgroundWorker StartBackgroundThread(DoWorkEventHandler doWorkEventHandler)
        {
            var backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.DoWork += doWorkEventHandler;
            backgroundWorker.RunWorkerAsync();
            return backgroundWorker;
        }

        public void StopThreads()
        {
            _SyncWorker.CancelAsync();
            _OFDMWorker.CancelAsync();
            _FICParserWorker.CancelAsync();
            _MSCDataParserWorker.CancelAsync();
            _DABWorker.CancelAsync();
            _statusWorker.CancelAsync();
        }

        public double PercentSignalPower
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// Inform that all data from input has been processed
        /// </summary>
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

        private FComplex[] GetSamples(int count, int phase, int msTimeOut = 1000)
        {
            var getStart = DateTime.Now;
            var res = new FComplex[count];

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
                            Thread.Sleep(MinThreadNoDataMSDelay);
                        }

                        continue;
                    } else
                    {
                        _currentSamplesPosition = 0;
                    }
                }
                res[i] = _currentSamples[_currentSamplesPosition];

                _localPhase -= phase;
                _localPhase = (_localPhase + Samplerate) % Samplerate;

                res[i] = FComplex.Multiply(res[i], _oscillatorTable[_localPhase]);

                _sLevel = Convert.ToSingle(0.00001 * res[i].L1Norm() + (1.0 - 0.00001) * _sLevel);

                i++;
                _currentSamplesPosition++;

                _totalSamplesRead++;
            }

            return res;
        }

        private string StatTitle(string title)
        {
            return $"--------{title.PadRight(45, '-')}";
        }

        private string StatValue(string title, string value, string unit)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                title += ":";
            }
            return $" {title.PadRight(25, ' ')} {value.PadLeft(15, ' ')} {unit}";
        }

        private string FormatStatValue(string title, int value, string unit)
        {
            return StatValue(title, value.ToString(), unit);
        }

        private string FormatStatValue(string title, double value, string unit)
        {
            return StatValue(title, value.ToString("N2"), unit);
        }

        private string FormatStatValue(string title, TimeSpan value, string unit)
        {
            var elapsed = DateTime.Now - _startTime;
            var time = $"{elapsed.Hours.ToString().PadLeft(2, '0')}:{elapsed.Minutes.ToString().PadLeft(2, '0')}:{elapsed.Seconds.ToString().PadLeft(2, '0')}";
            return StatValue(title, time, unit);
        }

        public void Stat(bool detailed)
        {
            _loggingService.Debug(StatTitle("-Queues-"));
            _loggingService.Debug(FormatStatValue("Samples", _samplesQueue.Count, "bs"));
            _loggingService.Debug(FormatStatValue("Data", _OFDMDataQueue.Count, "bs"));
            _loggingService.Debug(FormatStatValue("FIC", _ficDataQueue.Count, "bs"));
            _loggingService.Debug(FormatStatValue("MSC", _MSCDataQueue.Count, "bs"));
            _loggingService.Debug(FormatStatValue("DAB", _DABQueue.Count, "bs"));
            _loggingService.Debug(StatTitle("-Threads-"));
            _loggingService.Debug(FormatStatValue("Sync worker", _SyncWorkerTime, "ms"));
            if (detailed)
            {
                _loggingService.Debug(FormatStatValue("   Sync", _syncTime, "ms"));
                _loggingService.Debug(FormatStatValue("     (Continued count", _totalContinuedCount, ")"));
                _loggingService.Debug(FormatStatValue("   Find first symbol", _findFirstSymbolTotalTime, "ms"));
                _loggingService.Debug(FormatStatValue("   Get first symbol", _getFirstSymbolDataTotalTime, "ms"));
                _loggingService.Debug(FormatStatValue("   Coarse corrector", _coarseCorrectorTime, "ms"));
                _loggingService.Debug(FormatStatValue("   Get all symbols", _getAllSymbolsTime, "ms"));
                _loggingService.Debug(FormatStatValue("   Get NULL symbols", _getNULLSymbolsTime, "ms"));
            }
            _loggingService.Debug(FormatStatValue("OFDM worker", _OFDMDataTime, "ms"));
            _loggingService.Debug(FormatStatValue("FIC", _FICTime, "ms"));
            _loggingService.Debug(FormatStatValue("MSC", _MSCTime, "ms"));
            _loggingService.Debug(FormatStatValue("DAB", _DABTime, "ms"));
            if (detailed)
            {
                _loggingService.Debug(StatTitle("-FFT-"));
                _loggingService.Debug(FormatStatValue("ReorderData", Fourier.TotalFFTReorderDataTimeMs, "ms"));
                _loggingService.Debug(FormatStatValue("FFT", Fourier.TotalFFTTimeMs, "ms"));
                _loggingService.Debug(FormatStatValue("DFT", Fourier.TotalDFTTimeMs, "ms"));
            }
            _loggingService.Debug(StatTitle("-FIC-"));
            _loggingService.Debug(StatValue("Total", _fic.FICCount.ToString(), ""));
            _loggingService.Debug(StatValue("   Valid", _fic.FICCountWithValidCRC.ToString(), ""));
            _loggingService.Debug(StatValue("   InValid", _fic.FICCountWithInValidCRC.ToString(), ""));
            if (detailed)
            {
                foreach (var fig in _fic.FigTypesFound)
                {
                    _loggingService.Debug(StatValue($"#{fig.Key}", fig.Value.ToString(), ""));
                }
            }
            if (detailed)
            {
                _loggingService.Debug(StatTitle("-Services-"));
                foreach (var service in _fic.Services)
                {
                    var name = string.IsNullOrEmpty(service.ServiceName) ? "???" : service.ServiceName;
                    _loggingService.Debug(StatValue(name, service.ServiceNumber.ToString(), ""));
                }
            }
            _loggingService.Debug(StatTitle("-Total-"));
            _loggingService.Debug(FormatStatValue("Time", DateTime.Now - _startTime, ""));
            _loggingService.Debug(StatTitle("-"));
        }

        /// <summary>
        /// Sync samples position
        /// </summary>
        /// <returns>sync position</returns>
        private bool Sync(bool firstSync)
        {
            float currentStrength = 0;
            var syncBufferIndex = 0;

            // process first T_F/2 samples  (see void OFDMProcessor::run())
            if (firstSync)
            {
                GetSamples(T_F / 2, 0);
            }

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
                    _syncEnvBuffer[syncBufferIndex] = sample.L1Norm();
                    currentStrength += _syncEnvBuffer[syncBufferIndex];
                    syncBufferIndex++;
                }

                // looking for the null level

                var counter = 0;
                var ok = true;

                while (currentStrength / 50 > 0.5F * _sLevel)
                {
                    var sample = GetSamples(1, _coarseCorrector + _fineCorrector)[0];
                    _syncEnvBuffer[syncBufferIndex] = Math.Abs(sample.Real) + Math.Abs(sample.Imaginary);

                    // Update the levels
                    currentStrength += _syncEnvBuffer[syncBufferIndex] - _syncEnvBuffer[(syncBufferIndex - 50) & _syncBufferMask];
                    syncBufferIndex = (syncBufferIndex + 1) & _syncBufferMask;

                    counter++;
                    if (counter > T_F)
                    {
                        // Not synced!
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                {
                    _totalContinuedCount++;
                    continue;
                }

                // looking for the end of the null period.

                counter = 0;
                ok = true;
                while (currentStrength / 50 < 0.75F * _sLevel)
                {
                    var sample = GetSamples(1, _coarseCorrector + _fineCorrector)[0];
                    _syncEnvBuffer[syncBufferIndex] = sample.L1Norm();
                    //  update the levels
                    currentStrength += _syncEnvBuffer[syncBufferIndex] - _syncEnvBuffer[syncBufferIndex - 50 & _syncBufferMask];
                    syncBufferIndex = syncBufferIndex + 1 & _syncBufferMask;
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
                    //totalContinuedCount++;
                    continue;
                }
                else
                {
                    synced = true;
                }
            }

            return synced;
        }

        private int FindIndex(FComplex[] rawSamples)
        {
            try
            {
                // rawSamples must remain intact to CoarseCorrector
                //var samples = FComplex.CloneComplexArray(rawSamples);
                var samples = new FComplex[rawSamples.Length];
                Array.Copy(rawSamples, samples, rawSamples.Length);

                Fourier.FFTBackward(samples);

                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = FComplex.Multiply(samples[i], _phaseTable.RefTable[i].Conjugated());
                }

                samples = Fourier.DFTBackward(samples, _sinCosTable.CosTable, _sinCosTable.SinTable);

                float factor = 1.0F / samples.Length;

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

        private void _SyncWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"SyncWorker starting");

            bool firstSync = true;
            try
            {
                while (!_SyncWorker.CancellationPending)
                {
                    var startSyncWorkerTime = DateTime.Now;

                    try
                    {
                        if (!_state.Synced)
                        {
                            var startSyncTime = DateTime.Now;
                            _state.Synced = Sync(firstSync);
                            firstSync = false;

                            _syncTime += (DateTime.Now - startSyncTime).TotalMilliseconds;

                            if (!_state.Synced)
                            {
                                _loggingService.Debug($"-[]-Sync failed!");
                                continue;
                            }
                        }

                        // find first sample

                        var startFirstSymbolSearchTime = DateTime.Now;

                        var samples = GetSamples(T_u, _coarseCorrector + _fineCorrector);

                        var startIndex = FindIndex(samples);

                        _cycles++;
                        _loggingService.Debug($"Cycles: {_cycles}");
                        if (_cycles % 8 == 0)
                        {

                        }

                        _findFirstSymbolTotalTime += (DateTime.Now - startFirstSymbolSearchTime).TotalMilliseconds;

                        if (startIndex == -1)
                        {
                            // not synced
                            _state.Synced = false;
                            continue;
                        }

                        var startGetFirstSymbolDataTime = DateTime.Now;

                        var firstOFDMBuffer = new FComplex[T_u];

                        Array.Copy(samples, startIndex, firstOFDMBuffer, 0, T_u - startIndex);

                        var missingSamples = GetSamples(startIndex, _coarseCorrector + _fineCorrector);

                        Array.Copy(missingSamples, 0, firstOFDMBuffer, T_u - startIndex, startIndex);

                        _getFirstSymbolDataTotalTime += (DateTime.Now - startGetFirstSymbolDataTime).TotalMilliseconds;

                        var startCoarseCorrectorTime = DateTime.Now;

                        // coarse corrector
                        if (CoarseCorrector)
                        {
                            if (_fic.FicDecodeRatioPercent < 50)
                            {
                                int correction = ProcessPRS(firstOFDMBuffer);
                                if (correction != 100)
                                {
                                    _coarseCorrector += correction * CarrierDiff;
                                    if (Math.Abs(_coarseCorrector) > 35 * 1000)
                                        _coarseCorrector = 0;
                                }
                            } else
                            {

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

                        _OFDMDataQueue.Enqueue(allSymbols);

                        var startGetNULLSymbolsTime = DateTime.Now;

                        // cpp always round down
                        _fineCorrector = Convert.ToInt16(Math.Truncate(_fineCorrector + 0.1 * FreqCorr.PhaseAngle() / Math.PI * (CarrierDiff / 2.0)));

                        // save NULL data:

                        var nullSymbol = GetSamples(T_null, _coarseCorrector + _fineCorrector);

                        if (_fineCorrector > CarrierDiff / 2)
                        {
                            _coarseCorrector += CarrierDiff;
                            _fineCorrector -= CarrierDiff;
                        }
                        else
                        if (_fineCorrector < -CarrierDiff / 2)
                        {
                            _coarseCorrector -= CarrierDiff;
                            _fineCorrector += CarrierDiff;
                        }

                        _getNULLSymbolsTime += (DateTime.Now - startGetNULLSymbolsTime).TotalMilliseconds;

                    }
                    catch (NoSamplesException)
                    {
                       //
                    }

                    _SyncWorkerTime += (DateTime.Now - startSyncWorkerTime).TotalMilliseconds;
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"SyncWorker finished");
        }

        private void _OFDMWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"OFDMWorker started");

            try
            {
                while (!_OFDMWorker.CancellationPending)
                {
                    List<FComplex[]> allSymbols;

                    var ok = _OFDMDataQueue.TryDequeue(out allSymbols);

                    if (!ok)
                    {
                        Thread.Sleep(MinThreadNoDataMSDelay);
                    }
                    else
                    {
                        var startOFDMTime = DateTime.Now;

                        if (ProcessingSubChannel != null)
                        {
                            ProcessOFDMData(allSymbols);
                        }

                        _OFDMDataTime += (DateTime.Now - startOFDMTime).TotalMilliseconds;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"OFDMWorker finished");
        }

        private void _MSCDataParserWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"MSDCDataParserWorker started");

            try
            {
                while (!_MSCDataParserWorker.CancellationPending)
                {
                    sbyte[] MSCData;

                    var ok = _MSCDataQueue.TryDequeue(out MSCData);

                    if (!ok)
                    {
                        Thread.Sleep(MinThreadNoDataMSDelay);
                        // no data
                    }
                    else
                    {
                        var startTime = DateTime.Now;

                        ProcessMSCData(MSCData);

                        _MSCTime += (DateTime.Now - startTime).TotalMilliseconds;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"MSDCDataParserWorker finished");
        }

        private void _DABWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"DABWorker started");

            try
            {
                while (!_DABWorker.CancellationPending)
                {
                    byte[] DABData;

                    var ok = _DABQueue.TryDequeue(out DABData);

                    if (!ok)
                    {
                        Thread.Sleep(MinThreadNoDataMSDelay);
                        // no data
                    }
                    else
                    {
                        if (ProcessingSubChannel == null || _DABDecoder == null)
                                continue;

                        var startTime = DateTime.Now;

                        _DABDecoder.Feed(DABData);

                        _DABTime += (DateTime.Now - startTime).TotalMilliseconds;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"DABWorker finished");
        }

        private void _FICParserWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"FICParserWorker started");

            try
            {
                while (!_FICParserWorker.CancellationPending)
                {
                    FICQueueItem ficData;

                    var ok = _ficDataQueue.TryDequeue(out ficData);

                    if (!ok)
                    {
                        Thread.Sleep(MinThreadNoDataMSDelay);
                        // no data
                    }
                    else
                    {
                        var startTime = DateTime.Now;

                        _fic.ParseData(ficData);

                        _FICTime += (DateTime.Now - startTime).TotalMilliseconds;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"FICParserWorker finished");
        }

        private void _statusWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug($"StatusWorker started");
            var lastQueueSizeNotifyTime = DateTime.MinValue;

            try
            {
                while (!_statusWorker.CancellationPending)
                {
                    if ((DateTime.Now - lastQueueSizeNotifyTime).TotalSeconds > 5)
                    {
                        lastQueueSizeNotifyTime = DateTime.Now;
                        Stat(false);
                    }

                    Thread.Sleep(MinThreadNoDataMSDelay);

                    if (_finish &&
                       (_samplesQueue.Count == 0) &&
                       (_OFDMDataQueue.Count == 0) &&
                       (_ficDataQueue.Count == 0) &&
                       (_MSCDataQueue.Count == 0) &&
                       (_DABQueue.Count == 0))
                    {
                        OnFinished(this, new EventArgs());
                        _finish = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }

            _loggingService.Debug($"StatusWorker finished");
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

        private void ProcessOFDMData(List<FComplex[]> allSymbols)
        {
            try
            {
                // processPRS:
                var phaseReference = allSymbols[0];

                Fourier.FFTBackward(phaseReference);

                var snr = 0.0;
                snr = 0.7 * snr + 0.3 * GetSnr(phaseReference);

                // decodeDataSymbol:

                var iBits = new sbyte[K * 2];
                var mscData = new List<sbyte>();

                for (var sym = 1; sym < allSymbols.Count; sym++)
                {
                    var T_g = T_s - T_u;
                    var croppedSymbols = new FComplex[T_u];

                    Array.Copy(allSymbols[sym], T_g, croppedSymbols, 0, T_u);

                    Fourier.FFTBackward(croppedSymbols);

                    for (var i = 0; i < K; i++)
                    {
                        var index = _interleaver.MapIn(i);

                        if (index < 0)
                        {
                            index += T_u;
                        }

                        var r1 = FComplex.Multiply(croppedSymbols[index], phaseReference[index].Conjugated());
                        phaseReference[index] = croppedSymbols[index];

                        var ab1 = 127.0f / r1.L1Norm();
                        /// split the real and the imaginary part and scale it

                        var real = -r1.Real * ab1;
                        var imag = -r1.Imaginary * ab1;

                        iBits[i] = (sbyte)(Math.Truncate(real));
                        iBits[K + i] = (sbyte)(Math.Truncate(imag));
                    }

                    // values in iBits are changing during data processing!
                    if (sym < 4)
                    {
                        _ficDataQueue.Enqueue(new FICQueueItem()
                        {
                            Data = iBits.CloneArray(),
                            FicNo = sym - 1
                        });
                    }
                    else
                    {
                        mscData.AddRange(iBits.CloneArray());
                    }
                }

                _MSCDataQueue.Enqueue(mscData.ToArray());
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

            // MSCData consist of 72 symbols
            // 72 symbols ~ 211 184 bits  (27 648 bytes)
            // 72 symbols devided to 4 CIF (18 symbols)

            var startPos = Convert.ToInt32(ProcessingSubChannel.StartAddr * CUSize);
            var length = Convert.ToInt32(ProcessingSubChannel.Length * CUSize);

            if (_DABDecoder == null)
            {
                _DABDecoder = new DABDecoder(_loggingService,ProcessingSubChannel, CUSize, _DABQueue, OnDemodulated);
            }

            // dab-audio.run

            for (var cif = 0; cif < 4; cif++)
            {
                var DABBuffer = new sbyte[length];

                Buffer.BlockCopy(MSCData, cif * BitsperBlock * DABModeINumberOfBlocksPerCIF + startPos, DABBuffer, 0, length);

                _DABDecoder.ProcessCIFFragmentData(DABBuffer);
            }
        }

        private short GetSnr(FComplex[] v)
        {
            const int lowOffset = 70;
            const int highOffset = 20;
            const int noiseSamples1 = 90;
            const int noiseSamples2 = 100;
            const int signalSamples = 2 * (T_u / 4); // K/2 = T_u/2

            double noise = 0;
            double signal = 0;

            for (int i = lowOffset; i < lowOffset + noiseSamples1; i++)
            {
                noise += v[(T_u / 2 + i) % T_u].Abs();
                noise += v[(T_u / 2 + highOffset + i) % T_u].Abs();
            }

            noise /= noiseSamples1 + noiseSamples2;

            for (int i = -signalSamples / 2; i < signalSamples / 2; i++)
            {
                signal += v[(T_u / 2 + i) % T_u].Abs();
            }

            var dB_signal_new = GetDBOver256(signal / (signalSamples / 2.0));
            var dB_noise_new = GetDBOver256(noise);
            var snr_new = dB_signal_new - dB_noise_new;

            return (short)snr_new;
        }

        private double GetDBOver256(double x)
        {
            return 10 * Math.Log10(x);
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
                    Math.Cos(2.0 * Math.PI * i / (float)Samplerate),
                    Math.Sin(2.0 * Math.PI * i / (float)Samplerate));
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
