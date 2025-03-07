﻿using RTLSDR.Common;
using System;
using System.ComponentModel;
namespace RTLSDR.DAB
{
    public class DABProcessorState
    {
        public bool Synced { get; set; } = false;

        public AudioDataDescription AudioDescription { get; set; } = new AudioDataDescription();

        public DateTime StartTime { get; set; } = DateTime.MinValue;
        public double FindFirstSymbolTotalTime { get; set; } = 0;
        public double FindFirstSymbolFFTTime { get; set; } = 0;
        public double FindFirstSymbolDFTTime { get; set; } = 0;
        public double FindFirstSymbolMultiplyTime { get; set; } = 0;
        public double FindFirstSymbolBinTime { get; set; } = 0;
        public double GetFirstSymbolDataTotalTime { get; set; } = 0;
        public double SyncTotalTime { get; set; } = 0;
        public double GetAllSymbolsTime { get; set; } = 0;
        public double CoarseCorrectorTime { get; set; } = 0;
        public double GetNULLSymbolsTime { get; set; } = 0;

        public int TotalCyclesCount { get; set; } = 0;
        public bool FirstSyncProcessed { get; set; } = true;

        public float SLevel { get; set; } = 0;
        public int LocalPhase { get; set; } = 0;

        public short FineCorrector { get; set; } = 0;
        public int CoarseCorrector { get; set; } = 0;

        public DateTime LastSyncNotifyTime { get; set; } = DateTime.MinValue;
        public DateTime LastStatNotifyTime { get; set; } = DateTime.MinValue;

        public int TotalContinuedCount { get; set; } = 0;

        public double AudioBitrate { get; set; } = 0;
        public double IQBitrate { get; set; } = 0;
        public double SignalPower { get; set; } = 0;

        public int FICCount { get; set; } = 0;
        public int FICCountInValid { get; set; } = 0;
        public int FICCountValid { get; set; } = 0;

        public int ProcessedSuperFramesCount { get; set; } = 0;
        public int ProcessedSuperFramesCountInValid { get; set; } = 0;
        public int ProcessedSuperFramesCountValid { get; set; } = 0;

        public int ProcessedSuperFramesAUsCount { get; set; } = 0;
        public int ProcessedSuperFramesAUsCountInValid { get; set; } = 0;
        public int ProcessedSuperFramesAUsCountValid { get; set; } = 0;

        private byte _addSamplesOddByte;
        private bool _addSamplesOddByteSet = false;
        private double _power = 0;
        private DateTime _lastPowerCalculation = DateTime.MinValue;

        public IThreadWorkerInfo SyncThreadStat { get; set; } = null;
        public IThreadWorkerInfo SprectrumThreadStat { get; set; } = null;
        public IThreadWorkerInfo OFDMThreadStat { get; set; } = null;
        public IThreadWorkerInfo MSCThreadStat { get; set; } = null;
        public IThreadWorkerInfo FICThreadStat { get; set; } = null;
        public IThreadWorkerInfo SFMThreadStat { get; set; } = null;
        public IThreadWorkerInfo AACThreadStat { get; set; } = null;

        public String SyncedAsString
        {
            get
            {
                return Synced ? "Yes" : "-";
            }
        }


        private static string GetValueHR(double value)
        {
            if (value > 1000000)
            {
                return (value / 1000000.00).ToString("N2");
            }
            if (value > 1000)
            {
                return (value / 1000.00).ToString("N2");
            }

            return (value).ToString("N2");
        }

        private static string GetValueHRUnit(double value, string suffix)
        {
            if (value > 1000000)
            {
                return $"M{suffix}";
            }
            if (value > 1000)
            {
                return $"K{suffix}";
            }

            return $"{suffix}";
        }

        public string AudioDescriptionHR
        {
            get
            {
                if (AudioDescription == null)
                    return "";

                var sr = $"{GetValueHR(AudioDescription.SampleRate)} {GetValueHRUnit(AudioDescription.SampleRate, "Hz")}, {AudioDescription.BitsPerSample} bit";

                if (AudioDescription.Channels == 1)
                {
                    sr += ", mono";
                }
                else if (AudioDescription.Channels == 2)
                {
                    sr += ", stereo";
                }
                else
                {
                    sr += $", channels: {(AudioDescription.Channels)}";
                }

                return sr;
            }
        }

        public string AudioBitRateHR
        {
            get
            {
                return GetValueHR(AudioBitrate);
            }
        }

        public string AudioBitRateHRUnit
        {
            get
            {
                return GetValueHRUnit(AudioBitrate, "b/s");
            }
        }


        public string SignalPowerHR
        {
            get
            {
                return SignalPower.ToString("N0");
            }
        }

        public string SignalPowerHRUnit
        {
            get
            {
                return "%";
            }
        }


        public string IQBitRateHR
        {
            get
            {
                return GetValueHR(IQBitrate);
            }
        }

        public string IQBitRateHRUnit
        {
            get
            {
                return GetValueHRUnit(IQBitrate, "b/s");
            }
        }

        public void Clear()
        {
            Synced = false;
            AudioDescription = new AudioDataDescription();
            StartTime = DateTime.MinValue;
            FindFirstSymbolTotalTime = 0;
            FindFirstSymbolFFTTime = 0;
            FindFirstSymbolDFTTime = 0;
            FindFirstSymbolMultiplyTime  = 0;
            FindFirstSymbolBinTime  = 0;
            GetFirstSymbolDataTotalTime  = 0;
            SyncTotalTime  = 0;
            GetAllSymbolsTime  = 0;
            CoarseCorrectorTime  = 0;
            GetNULLSymbolsTime  = 0;

            TotalCyclesCount  = 0;
            FirstSyncProcessed  = true;

            SLevel = 0;
            LocalPhase = 0;

            FineCorrector = 0;
            CoarseCorrector = 0;

            LastSyncNotifyTime = DateTime.MinValue;
            LastStatNotifyTime = DateTime.MinValue;

            TotalContinuedCount  = 0;

            AudioBitrate  = 0;
            IQBitrate  = 0;
            SignalPower  = 0;

            FICCount  = 0;
            FICCountInValid  = 0;
            FICCountValid  = 0;

            ProcessedSuperFramesCount  = 0;
            ProcessedSuperFramesCountInValid  = 0;
            ProcessedSuperFramesCountValid  = 0;

            ProcessedSuperFramesAUsCount  = 0;
            ProcessedSuperFramesAUsCountInValid  = 0;
            ProcessedSuperFramesAUsCountValid  = 0;
        }
    }
}
