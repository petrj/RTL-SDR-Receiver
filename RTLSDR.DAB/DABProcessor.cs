using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using LoggerService;
using RTLSDR.Common;

namespace RTLSDR.DAB
{
    /*
        Free .NET DAB+ library

        -   based upon welle.io (https://github.com/AlbrechtL/welle.io)
        -   DAB documentation: https://www.etsi.org/deliver/etsi_en/300400_300499/300401/02.01.01_60/en_300401v020101p.pdf
    */

    public class DABProcessor : IDemodulator
    {
        private ILoggingService _loggingService;

        public int Samplerate { get; set; } = 2048000; // INPUT_RATE
        public bool CoarseCorrector { get; set; } = true;

        public event EventHandler OnDemodulated = null;
        public event EventHandler OnFinished = null;
        public event EventHandler OnServiceFound = null;
        public event EventHandler OnServicePlayed = null;

        public int ServiceNumber { get; set; } = -1;

        private DABSubChannel _processingSubChannel { get; set; } = null;
        private DABService _processingService { get; set; } = null;

        private DABProcessorState _state = new DABProcessorState();

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
        private ConcurrentQueue<byte[]> _DABSuperFrameDataQueue = new ConcurrentQueue<byte[]>();
        private ConcurrentQueue<byte[]> _AACDataQueue = new ConcurrentQueue<byte[]>();

        private ThreadWorker<object> _statusThreadWorker = null;
        private ThreadWorker<FComplex[]> _syncThreadWorker = null;
        private ThreadWorker<List<FComplex[]>> _OFDMThreadWorker = null;   // FFT
        private ThreadWorker<FICQueueItem> _FICThreadWorker = null;        // Reading FIC channel
        private ThreadWorker<sbyte[]> _MSCThreadWorker = null;             // Reading MSC channel (de-interleave, deconvolute, dedisperse)
        private ThreadWorker<byte[]> _SuperFrameThreadWorker = null;       // Decoding SuperFrames
        private ThreadWorker<byte[]> _AACThreadWorker = null;              // AAC to PCM

        private FComplex[] _currentSamples = null;
        private int _currentSamplesPosition = -1;
        private long _totalSamplesRead = 0;

        private const int SyncBufferSize = 32768;
        private const int SyncInterruptCyclesCount = 100;
        private float[] _syncEnvBuffer = new float[SyncBufferSize];
        private int _syncBufferMask = SyncBufferSize - 1;

        private FrequencyInterleaver _interleaver;
        private IAACDecoder _aacDecoder = null;

        private BitRateCalculation _audioBitRateCalculator;
        private BitRateCalculation _IQBitRateCalculator;

        // DAB mode I:
        private const int DABModeINumberOfBlocksPerCIF = 18;

        private FComplex[] _oscillatorTable { get; set; } = null;
        private double[] _refArg;
        private FourierSinCosTable _sinCosTable = null;

        private PhaseTable _phaseTable = null;
        private FICData _fic;

        private Viterbi _FICViterbi;

        private DABDecoder _DABDecoder = null;

        private byte _addSamplesOoddByte;
        private bool _oddByteSet = false;

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
            _fic.OnServiceFound += _fic_OnServiceFound;
            _fic.OnProcessedFICCountChanged += delegate
            {
                _state.FICCount = _fic.FICCount;
                _state.FICCountValid = _fic.FICProcessedCountWithValidCRC;
                _state.FICCountInValid = _fic.FICProcessedCountWithInValidCRC;
                _state.FICCountInValid = _fic.FICProcessedCountWithInValidCRC;
            };

            _state.StartTime = DateTime.Now;

            _statusThreadWorker = new ThreadWorker<object>(_loggingService, "STAT");
            _statusThreadWorker.SetThreadMethod(StatusThreadWorkerGo, MinThreadNoDataMSDelay);
            //_statusThreadWorker.Start();

            _syncThreadWorker = new ThreadWorker<FComplex[]>(_loggingService, "SYNC");
            _syncThreadWorker.SetThreadMethod(SyncThreadWorkerGo, MinThreadNoDataMSDelay);
            _syncThreadWorker.SetQueue(_samplesQueue);
            //_syncThreadWorker.Start();

            _OFDMThreadWorker = new ThreadWorker<List<FComplex[]>>(_loggingService, "OFDM");
            _OFDMThreadWorker.SetThreadMethod(_OFDMThreadWorkerGo, MinThreadNoDataMSDelay);
            _OFDMThreadWorker.SetQueue(_OFDMDataQueue);
            _OFDMThreadWorker.ReadingQueue = true;
            //_OFDMThreadWorker.Start();

            _FICThreadWorker = new ThreadWorker<FICQueueItem>(_loggingService, "FIC");
            _FICThreadWorker.SetThreadMethod(FICThreadWorkerGo, MinThreadNoDataMSDelay);
            _FICThreadWorker.SetQueue(_ficDataQueue);
            _FICThreadWorker.ReadingQueue = true;
            //_FICThreadWorker.Start();

            _MSCThreadWorker = new ThreadWorker<sbyte[]>(_loggingService, "MSC");
            _MSCThreadWorker.SetThreadMethod(MSCThreadWorkerGo, MinThreadNoDataMSDelay);
            _MSCThreadWorker.SetQueue(_MSCDataQueue);
            _MSCThreadWorker.ReadingQueue = true;
            //_MSCThreadWorker.Start();

            _SuperFrameThreadWorker = new ThreadWorker<byte[]>(_loggingService, "SpFM");
            _SuperFrameThreadWorker.SetThreadMethod(SuperFrameThreadWorkerGo, MinThreadNoDataMSDelay);
            _SuperFrameThreadWorker.SetQueue(_DABSuperFrameDataQueue);
            _SuperFrameThreadWorker.ReadingQueue = true;
            //_SuperFrameThreadWorker.Start();

            _AACThreadWorker = new ThreadWorker<byte[]>(_loggingService, "AAC");
            _AACThreadWorker.SetThreadMethod(AACThreadWorkerGo, MinThreadNoDataMSDelay);
            _AACThreadWorker.SetQueue(_AACDataQueue);
            _AACThreadWorker.ReadingQueue = true;
            //_AACThreadWorker.Start();

            _state.SyncThreadStat = _syncThreadWorker;
            _state.OFDMThreadStat = _OFDMThreadWorker;
            _state.MSCThreadStat = _MSCThreadWorker;
            _state.FICThreadStat = _FICThreadWorker;
            _state.SFMThreadStat = _SuperFrameThreadWorker;
            _state.AACThreadStat = _AACThreadWorker;

            _audioBitRateCalculator = new BitRateCalculation(_loggingService, "DAB audio");
            _IQBitRateCalculator =  new BitRateCalculation(_loggingService, "IQ data");
        }

        public void Start()
        {
            _loggingService.Debug("Starting all thread workers");

            _statusThreadWorker.Start();
            _syncThreadWorker.Start();
            _OFDMThreadWorker.Start();
            _FICThreadWorker.Start();
            _MSCThreadWorker.Start();
            _SuperFrameThreadWorker.Start();
            _AACThreadWorker.Start();
        }

        public void Stop()
        {
            _loggingService.Debug("Stopping all thread workers");

            _syncThreadWorker.Stop();
            _statusThreadWorker.Stop();
            _OFDMThreadWorker.Stop();
            _FICThreadWorker.Stop();
            _MSCThreadWorker.Stop();
            _SuperFrameThreadWorker.Stop();
            _AACThreadWorker.Stop();
        }

        public int QueueSize
        {
            get
            {
                var res = 0;
                if ((_syncThreadWorker != null) &&
                    (_syncThreadWorker is IThreadWorkerInfo i)
                   )
                {
                    res += i.QueueItemsCount;
                }

                return res;
            }
        }

        public double AudioBitrate
        {
            get
            {
                return _state.AudioBitrate;
            }
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

        public DABService ProcessingDABService
        {
            get
            {
                return _processingService;
            }
        }

        public DABProcessorState State
        {
            get
            {
                return _state;
            }
        }

        public DABSubChannel ProcessingSubCannel
        {
            get
            {
                return _processingSubChannel;
            }
        }

        #region STAT

        private string StatTitle(string title)
        {
            return $"--------{title.PadRight(46, '-')}";
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

        private string FormatStatValue(string title, bool value)
        {
            return StatValue(title, value ? "[x]" : "[ ]", String.Empty);
        }

        private string FormatStatValue(string title, double value, string unit)
        {
            return StatValue(title, value.ToString("N2"), unit);
        }

        private string FormatStatValue(string title, TimeSpan value, string unit)
        {
            var elapsed = DateTime.Now - _state.StartTime;
            var time = $"{elapsed.Hours.ToString().PadLeft(2, '0')}:{elapsed.Minutes.ToString().PadLeft(2, '0')}:{elapsed.Seconds.ToString().PadLeft(2, '0')}";
            return StatValue(title, time, unit);
        }

        public string Stat(bool detailed)
        {
            var res = new StringBuilder();

            var line = "";

            if (_IQBitRateCalculator!=null)
            {
                line = $"{" BitRate - IQ".PadRight(34, ' ')}";
                line += $"{ _IQBitRateCalculator.BitRateAsString.PadLeft(16, ' ')}";
                res.AppendLine(line);
            }

            if (_audioBitRateCalculator!=null)
            {
                line = $"{" BitRate - AAC".PadRight(34, ' ')}";
                line += $"{ _audioBitRateCalculator.BitRateAsString.PadLeft(16, ' ')}";
                res.AppendLine(line);
            }

            line = $"{"-Thread-".PadLeft(9, '-')}";
            line += $"{"-Queue-".PadLeft(17, '-')}";
            line += $"{"-Cycles-".PadLeft(12, '-')}";
            line += $"{"-Time(s)-".PadLeft(17, '-')}";

            res.AppendLine(line);

            var tws = new List<IThreadWorkerInfo>();
            tws.AddRange(new IThreadWorkerInfo[]
            {
            _syncThreadWorker,
            _OFDMThreadWorker,
            _MSCThreadWorker,
            _FICThreadWorker,
            _SuperFrameThreadWorker,
            _AACThreadWorker,
            });

            var sumCount = 0;
            foreach (var twi in tws)
            {
                if (twi == null)
                    continue;
                line = $"{(twi.Name).ToString().PadLeft(8, ' ')} |";
                line += $"{(twi.QueueItemsCount.ToString().PadLeft(15, ' '))} |";
                line += $"{twi.CyclesCount.ToString().PadLeft(10, ' ')} |";
                line += $"{(twi.WorkingTimeMS / 1000).ToString("#00.00").PadLeft(15, ' ')} |";
                sumCount += twi.QueueItemsCount;
                res.AppendLine(line);
            }
            line = $"{"-Total-".PadLeft(9, '-')}";
            line += $"{"-".PadLeft(17, '-')}";
            line += $"{"-".PadLeft(12, '-')}";
            line += $"{"-" + (((DateTime.Now - _state.StartTime).TotalMilliseconds / 1000).ToString("#00.00") + "-").PadLeft(16, '-')}";
            res.AppendLine(line);

            line = $"{"-".PadLeft(9, '-')}";
            line += $"{"-Total-".PadLeft(17, '-')}";
            line += $"{"-Invalid-".PadLeft(12, '-')}";
            line += $"{"-Decoded-".PadLeft(17, '-')}";
            res.AppendLine(line);

            line = $"{"FIC".PadLeft(8, ' ')} |";
            line += $"{_fic.FICCount.ToString().PadLeft(15, ' ')} |";
            line += $"{_fic.FICProcessedCountWithInValidCRC.ToString().PadLeft(10, ' ')} |";
            line += $"{_fic.FICProcessedCountWithValidCRC.ToString().PadLeft(15, ' ')} |";
            res.AppendLine(line);

            if (_DABDecoder != null)
            {
                line = $"{"SpFS".PadLeft(8, ' ')} |";
                line += $"{_DABDecoder.ProcessedSuperFramesCount.ToString().PadLeft(15, ' ')} |";
                line += $"{(_DABDecoder.ProcessedSuperFramesCount - _DABDecoder.ProcessedSuperFramesSyncedCount).ToString().PadLeft(10, ' ')} |";
                line += $"{_DABDecoder.ProcessedSuperFramesSyncedCount.ToString().PadLeft(15, ' ')} |";
                res.AppendLine(line);

                line = $"{"AU".PadLeft(8, ' ')} |";
                line += $"{_DABDecoder.ProcessedSuperFramesAUsCount.ToString().PadLeft(15, ' ')} |";
                line += $"{(_DABDecoder.ProcessedSuperFramesAUsCount - _DABDecoder.ProcessedSuperFramesAUsSyncedCount).ToString().PadLeft(10, ' ')} |";
                line += $"{_DABDecoder.ProcessedSuperFramesAUsSyncedCount.ToString().PadLeft(15, ' ')} |";
                res.AppendLine(line);
            }

            res.AppendLine(StatTitle("-"));

            line = $"{" Synced".PadRight(12, ' ')}";
            line += $"{ (_state.Synced ? "[x]" : "[ ]").PadLeft(10, ' ')}";
            line += $"{"    Continued count".PadRight(20, ' ')}";
            line += $"{ _state.TotalContinuedCount.ToString().PadLeft(12, ' ')}";
            res.AppendLine(line);

            if (detailed)
            {
                res.AppendLine(StatTitle("-"));
                res.AppendLine(FormatStatValue("   SLevel", _state.SLevel, ""));
                res.AppendLine(FormatStatValue("   LocalPhase", _state.LocalPhase, ""));
                res.AppendLine(FormatStatValue("   Sync time", _state.SyncTotalTime, "ms"));
                res.AppendLine(FormatStatValue("   Find first symbol", _state.FindFirstSymbolTotalTime, "ms"));
                res.AppendLine(FormatStatValue("     (FFT           ", _state.FindFirstSymbolFFTTime, "ms)"));
                res.AppendLine(FormatStatValue("     (DFT           ", _state.FindFirstSymbolDFTTime, "ms)"));
                res.AppendLine(FormatStatValue("     (Multiply      ", _state.FindFirstSymbolMultiplyTime, "ms)"));
                res.AppendLine(FormatStatValue("     (Bin           ", _state.FindFirstSymbolBinTime, "ms)"));
                res.AppendLine(FormatStatValue("   Get first symbol", _state.GetFirstSymbolDataTotalTime, "ms"));
                res.AppendLine(FormatStatValue("   Coarse corrector", _state.CoarseCorrectorTime, "ms"));
                res.AppendLine(FormatStatValue("   Get all symbols", _state.GetAllSymbolsTime, "ms"));
                res.AppendLine(FormatStatValue("   Get NULL symbols", _state.GetNULLSymbolsTime, "ms"));

                res.AppendLine(StatTitle("-FFT-"));
                res.AppendLine(FormatStatValue("ReorderData", Fourier.TotalFFTReorderDataTimeMs, "ms"));
                res.AppendLine(FormatStatValue("FFT", Fourier.TotalFFTTimeMs, "ms"));
                res.AppendLine(FormatStatValue("DFT", Fourier.TotalDFTTimeMs, "ms"));

                res.AppendLine(StatTitle("-FIG-"));
                foreach (var fig in _fic.FigTypesFound)
                {
                    res.AppendLine(StatValue($"#{fig.Key}", fig.Value.ToString(), ""));
                }

                res.AppendLine(StatTitle("-Total-"));
                res.AppendLine(FormatStatValue("Time", DateTime.Now - _state.StartTime, ""));
                res.AppendLine(StatTitle("-"));
            }

            return res.ToString();
        }

        #endregion

        public void ResetSync()
        {
            _state.TotalContinuedCount = 0;
            _state.Synced = false;
            _state.FirstSyncProcessed = true;
            _state.CoarseCorrector = 0;
            _state.FineCorrector = 0;
            _state.SLevel = 0;
            _state.LocalPhase = 0;
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

                // break when total samples read exceed some value
                if ( _state.TotalContinuedCount>SyncInterruptCyclesCount)
                {
                    _loggingService.Info($"Syncing failed ({SyncInterruptCyclesCount} cycles)");
                    ResetSync();
                    return false;
                }

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

                while (currentStrength / 50 > 0.5F * _state.SLevel)
                {
                    var sample = GetSamples(1, _state.CoarseCorrector + _state.FineCorrector)[0];
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
                    _state.TotalContinuedCount++;
                    continue;
                }

                // looking for the end of the null period.

                counter = 0;
                ok = true;
                while (currentStrength / 50 < 0.75F * _state.SLevel)
                {
                    var sample = GetSamples(1, _state.CoarseCorrector + _state.FineCorrector)[0];

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
                     _state.TotalContinuedCount++;
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

                var samples = new FComplex[rawSamples.Length];
                Array.Copy(rawSamples, samples, rawSamples.Length);

                //var samples = FComplex.CloneComplexArray(rawSamples);
                //var samples = rawSamples.Clone() as FComplex[];

                var startFindFirstSymbolFFTTime = DateTime.Now;

                Fourier.FFTBackward(samples);

                _state.FindFirstSymbolFFTTime += (DateTime.Now - startFindFirstSymbolFFTTime).TotalMilliseconds;

                var startFindFirstSymbolMultiplyTime = DateTime.Now;

                for (var i = 0; i < samples.Length; i++)
                {
                    //samples[i] = FComplex.Multiply(samples[i], _phaseTable.RefTable[i].Conjugated());
                    samples[i] = FComplex.MultiplyConjugated(samples[i], _phaseTable.RefTable[i]);
                }

                _state.FindFirstSymbolMultiplyTime += (DateTime.Now - startFindFirstSymbolMultiplyTime).TotalMilliseconds;

                var startFindFirstSymbolDFTTime = DateTime.Now;

                samples = Fourier.DFTBackward(samples, _sinCosTable.CosTable, _sinCosTable.SinTable);

                _state.FindFirstSymbolDFTTime += (DateTime.Now - startFindFirstSymbolDFTTime).TotalMilliseconds;

                float factor = 1.0F / samples.Length;

                startFindFirstSymbolMultiplyTime = DateTime.Now;

                //// scale all entries
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i].Scale(factor);
                }

                _state.FindFirstSymbolMultiplyTime += (DateTime.Now - startFindFirstSymbolMultiplyTime).TotalMilliseconds;

                //var impulseResponseBuffer = new List<double>();
                //for (var impulseResponseBufferIter = 0; impulseResponseBufferIter < samples.Length; impulseResponseBufferIter++)
                //{
                //    impulseResponseBuffer.Add(0);
                //}

                // FFTPlacementMethod::EarliestPeakWithBinning:

                var startFindFirstSymbolBinTime = DateTime.Now;

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
                        //impulseResponseBuffer[i + j] = value;

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

                _state.FindFirstSymbolBinTime += (DateTime.Now - startFindFirstSymbolBinTime).TotalMilliseconds;

                return earliestPeak.Index;

            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, "Error finding index");
                return -1;
            }
        }

        public bool Synced
        {
            get
            {
                if (_state == null)
                return false;

                return _state.Synced;
            }
        }

        private void SyncThreadWorkerGo(object data = null)
        {
            try
            {
                _state.TotalCyclesCount++;
                if ((DateTime.Now - _state.LastSyncNotifyTime).TotalSeconds > 1)
                {
                    //_loggingService.Debug($" Sync: cycle: {_cycles.ToString().PadLeft(3, ' ')}, synced: {_state.Synced}");
                    _state.LastSyncNotifyTime = DateTime.Now;
                }

                if (!_state.Synced)
                {
                    var startSyncTime = DateTime.Now;
                    _state.Synced = Sync(_state.FirstSyncProcessed);
                    _state.FirstSyncProcessed = false;

                    _state.SyncTotalTime += (DateTime.Now - startSyncTime).TotalMilliseconds;

                    if (!_state.Synced)
                    {
                        _loggingService.Debug($"-[]-Sync failed!");
                        return;
                    }
                }

                // find first sample

                var samples = GetSamples(T_u, _state.CoarseCorrector + _state.FineCorrector);

                var startFirstSymbolSearchTime = DateTime.Now;

                var startIndex = FindIndex(samples);

                _state.FindFirstSymbolTotalTime += (DateTime.Now - startFirstSymbolSearchTime).TotalMilliseconds;

                if (startIndex == -1)
                {
                    // not synced
                    _state.Synced = false;
                    return;
                }

                var startGetFirstSymbolDataTime = DateTime.Now;

                var firstOFDMBuffer = new FComplex[T_u];

                Array.Copy(samples, startIndex, firstOFDMBuffer, 0, T_u - startIndex);

                var missingSamples = GetSamples(startIndex, _state.CoarseCorrector + _state.FineCorrector);

                Array.Copy(missingSamples, 0, firstOFDMBuffer, T_u - startIndex, startIndex);

                _state.GetFirstSymbolDataTotalTime += (DateTime.Now - startGetFirstSymbolDataTime).TotalMilliseconds;

                var startCoarseCorrectorTime = DateTime.Now;

                // coarse corrector
                if (CoarseCorrector)
                {
                    if (_fic.FicDecodeRatioPercent < 50)
                    {
                        int correction = ProcessPRS(firstOFDMBuffer);
                        if (correction != 100)
                        {
                            _state.CoarseCorrector += correction * CarrierDiff;
                            if (Math.Abs(_state.CoarseCorrector) > 35 * 1000)
                                _state.CoarseCorrector = 0;
                        }
                    }
                    else
                    {

                    }
                }

                _state.CoarseCorrectorTime += (DateTime.Now - startCoarseCorrectorTime).TotalMilliseconds;

                var startGetAllSymbolsTime = DateTime.Now;

                var allSymbols = new List<FComplex[]>();
                allSymbols.Add(firstOFDMBuffer);

                // ofdmBuffer.resize(params.L * params.T_s);

                var FreqCorr = new FComplex(0, 0);

                for (int sym = 1; sym < L; sym++)
                {
                    var buf = GetSamples(T_s, _state.CoarseCorrector + _state.FineCorrector);
                    allSymbols.Add(buf);

                    for (int i = T_u; i < T_s; i++)
                    {
                        FreqCorr.Add(FComplex.Multiply(buf[i], buf[i - T_u].Conjugated()));
                    }
                }

                _OFDMDataQueue.Enqueue(allSymbols);

                _state.GetAllSymbolsTime += (DateTime.Now - startGetAllSymbolsTime).TotalMilliseconds;

                var startGetNULLSymbolsTime = DateTime.Now;

                // cpp always round down
                _state.FineCorrector = Convert.ToInt16(Math.Truncate(_state.FineCorrector + 0.1 * FreqCorr.PhaseAngle() / Math.PI * (CarrierDiff / 2.0)));

                // save NULL data:

                var nullSymbol = GetSamples(T_null, _state.CoarseCorrector + _state.FineCorrector);

                if (_state.FineCorrector > CarrierDiff / 2)
                {
                    _state.CoarseCorrector += CarrierDiff;
                    _state.FineCorrector -= CarrierDiff;
                }
                else
                if (_state.FineCorrector < -CarrierDiff / 2)
                {
                    _state.CoarseCorrector -= CarrierDiff;
                    _state.FineCorrector += CarrierDiff;
                }

                _state.GetNULLSymbolsTime += (DateTime.Now - startGetNULLSymbolsTime).TotalMilliseconds;

            }
            catch (NoSamplesException)
            {
                //
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, "Error while sync");
            } finally
            {

            }
        }

        private void _OFDMThreadWorkerGo(List<FComplex[]> allSymbols)
        {
            // TODO: demodulate only if needed
            ProcessOFDMData(allSymbols);
        }

        private void MSCThreadWorkerGo(sbyte[] MSCData)
        {
            ProcessMSCData(MSCData);
        }

        private void AACThreadWorkerGo(byte[] AUData)
        {
            if ((_aacDecoder != null) && (OnDemodulated != null))
            {
                var pcmData = _aacDecoder.DecodeAAC(AUData);

                if (pcmData == null)
                {
                    _loggingService.Info("AAC decoding error");
                    return;
                }

                var audioDescription = new AudioDataDescription()
                {
                    BitsPerSample = 16,
                    Channels = 2,
                    SampleRate = 48000
                };

                OnDemodulated(this, new DataDemodulatedEventArgs()
                {
                    Data = pcmData,
                    AudioDescription = audioDescription
                });

                _state.AudioBitrate = _audioBitRateCalculator.UpdateBitRate(pcmData.Length);
                _state.AudioDescription = audioDescription;
            }
        }

        private void SuperFrameThreadWorkerGo(byte[] DABData)
        {
            if (_DABDecoder != null)
            {
                _DABDecoder.Feed(DABData);
            }
        }

        private void FICThreadWorkerGo(FICQueueItem ficData)
        {
            if (ficData.Data == null)
                return;

            _fic.ParseData(ficData);
        }

        private void StatusThreadWorkerGo(object input = null)
        {
            try
            {
                /*
                if ((DateTime.Now - _state.LastStatNotifyTime).TotalSeconds > 5)
                {
                    _state.LastStatNotifyTime = DateTime.Now;
                    _loggingService.Debug(Stat(true));
                }
                */

                if (_finish &&
                    (_samplesQueue.Count == 0) &&
                    (_OFDMDataQueue.Count == 0) &&
                    (_ficDataQueue.Count == 0) &&
                    (_MSCDataQueue.Count == 0) &&
                    (_DABSuperFrameDataQueue.Count == 0) &&
                    (_AACDataQueue.Count == 0))
                {
                    OnFinished(this, new EventArgs());
                    _finish = false;
                }
            }
            catch (Exception ex)
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

                if (ServiceNumber > 0)
                {
                    _MSCDataQueue.Enqueue(mscData.ToArray());
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        private void ProcessMSCData(sbyte[] MSCData)
        {
            if (_processingSubChannel == null)
                return;

            // MSCData consist of 72 symbols
            // 72 symbols ~ 211 184 bits  (27 648 bytes)
            // 72 symbols devided to 4 CIF (18 symbols)

            var startPos = Convert.ToInt32(_processingSubChannel.StartAddr * CUSize);
            var length = Convert.ToInt32(_processingSubChannel.Length * CUSize);

            if (_DABDecoder == null)
            {
                _DABDecoder = new DABDecoder(
                    _loggingService,
                    _processingSubChannel,
                    CUSize,
                    _DABSuperFrameDataQueue,
                    DABDecoder_OnDemodulated,
                    DABDecoder_OnSuperFrameHeaderDemodulated);

                _DABDecoder.OnProcessedSuperFramesChanged += _DABDecoder_OnProcessedSuperFramesChanged;
            }

            // dab-audio.run

            for (var cif = 0; cif < 4; cif++)
            {
                var DABBuffer = new sbyte[length];

                Buffer.BlockCopy(MSCData, cif * BitsperBlock * DABModeINumberOfBlocksPerCIF + startPos, DABBuffer, 0, length);

                if (_DABDecoder != null)
                {
                    _DABDecoder.ProcessCIFFragmentData(DABBuffer);
                }
            }
        }

        private void _DABDecoder_OnProcessedSuperFramesChanged(object? sender, EventArgs e)
        {
            if (_DABDecoder != null)
            {
                _state.ProcessedSuperFramesCount = _DABDecoder.ProcessedSuperFramesCount;
                _state.ProcessedSuperFramesCountInValid = _DABDecoder.ProcessedSuperFramesCount - _DABDecoder.ProcessedSuperFramesSyncedCount;
                _state.ProcessedSuperFramesCountValid = _DABDecoder.ProcessedSuperFramesSyncedCount;

                _state.ProcessedSuperFramesAUsCount = _DABDecoder.ProcessedSuperFramesAUsCount;
                _state.ProcessedSuperFramesAUsCountInValid = _DABDecoder.ProcessedSuperFramesAUsCount - _DABDecoder.ProcessedSuperFramesAUsSyncedCount;
                _state.ProcessedSuperFramesAUsCountValid = _DABDecoder.ProcessedSuperFramesAUsSyncedCount;
            }
        }

        private void DABDecoder_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs eAACdata)
            {
                _AACDataQueue.Enqueue(eAACdata.Data);
            }
        }

        private void DABDecoder_OnSuperFrameHeaderDemodulated(object sender, EventArgs e)
        {
            if (_aacDecoder == null)
            {
                if (e is AACSeperFrameHaderDemodulatedEventArgs eAAC)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        _aacDecoder = new AACDecoderWindows(_loggingService);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        _aacDecoder = new AACDecoderLinux(_loggingService);
                    } else
                    {
                        throw new NotImplementedException("Unlnown platform for AACDecoder initialization");
                    }

                    var aacDecoderinitStatus = _aacDecoder.Init(eAAC.Header);
                    _loggingService.Info($"AACDecoder started with status: {aacDecoderinitStatus}");
                }
            }
        }

        private void _fic_OnServiceFound(object sender, EventArgs e)
        {
            if (e is DABServiceFoundEventArgs d)
            {
                if (_processingSubChannel == null &&
                    d.Service.ServiceNumber == ServiceNumber)
                {
                    SetProcessingSubChannel(d.Service, d.Service.FirstSubChannel);
                }

                if (OnServiceFound != null)
                {
                    OnServiceFound(this, e);
                }
            }
        }

        public void SetProcessingService(IAudioService service)
        {
            if (service is DABService dabService)
            {
                SetProcessingSubChannel(dabService, dabService.FirstSubChannel);
            }
        }

        public void SetProcessingSubChannel(DABService service, DABSubChannel dABSubChannel)
        {
            _processingSubChannel = dABSubChannel;
            _processingService = service;
            ServiceNumber = Convert.ToInt32(service.ServiceNumber);
            _DABDecoder = null;
            if (_aacDecoder != null)
            {
                _aacDecoder.Close();
                _aacDecoder = null;
            }

            if (OnServicePlayed != null)
            {
                OnServicePlayed(this, new DABServicePlayedEventArgs()
                {
                    Service = service,
                    SubChannel = dABSubChannel
                });
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

        public static FComplex[] ToDSPComplex(byte[] iqData, int length, int offset)
        {
            var res = new FComplex[length / 2];

            float factor = 1.0f / 128.0f;

            for (int i = 0; i < length / 2; i++)
            {
                res[i] = new FComplex(
                                (iqData[i * 2 + offset] - 128) * factor,
                                (iqData[i * 2 + offset + 1] - 128) * factor
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
                        }
                        else
                        {
                            Thread.Sleep(MinThreadNoDataMSDelay);
                        }

                        continue;
                    }
                    else
                    {
                        _currentSamplesPosition = 0;
                    }
                }
                if (_currentSamplesPosition>_currentSamples.Length-1)
                {
                    throw new NoSamplesException();
                }
                res[i] = _currentSamples[_currentSamplesPosition];

                _state.LocalPhase -= phase;
                _state.LocalPhase = (_state.LocalPhase + Samplerate) % Samplerate;

                res[i] = FComplex.Multiply(res[i], _oscillatorTable[_state.LocalPhase]);
                _state.SLevel = Convert.ToSingle(0.00001 *(res[i].L1Norm()) + (1.0 - 0.00001) * _state.SLevel);

                /* no time gain
                var rr = res[i].Real;
                var ri = res[i].Imaginary;
                res[i].Real =       (rr * _oscillatorTable[_localPhase].Real - ri * _oscillatorTable[_localPhase].Imaginary);
                res[i].Imaginary =  (rr * _oscillatorTable[_localPhase].Imaginary + ri * _oscillatorTable[_localPhase].Real);

                _sLevel = Convert.ToSingle(0.00001 *(Math.Abs(res[i].Real)+Math.Abs(res[i].Imaginary)) + 0.99999 * _sLevel);
                */

                i++;
                _currentSamplesPosition++;

                _totalSamplesRead++;
            }

            return res;
        }

        public void AddSamples(byte[] IQData, int length)
        {
            int offset = 0;

            if (_oddByteSet)
            {
                var missingSample = ToDSPComplex( new byte[] {_addSamplesOoddByte , IQData[0]}, 2 , 0);
                offset = 1;
                 _samplesQueue.Enqueue(missingSample);
            }

            var dspComplexArray = ToDSPComplex(IQData, length-offset, offset);
            _samplesQueue.Enqueue(dspComplexArray);

            if (((length-offset) % 2) == 1)
            {
                _addSamplesOoddByte = IQData[length-1];
                _oddByteSet = true;
            } else
            {
                _oddByteSet = false;
            }

            _state.IQBitrate = _IQBitRateCalculator.UpdateBitRate(length);
        }
    }
}
