using RTLSDR.Common;
using System;
using System.ComponentModel;
namespace RTLSDR.DAB
{
    public class DABProcessorState
    {
        public bool Synced { get; set; } = false;

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

        public int ProcessedSuperFramesAUsSyncedDecodedCount { get; set; } = 0;

        public double AudioBitrate { get; set; } = 0;

        public string AudioBitRateHR
        {
            get
            {
                if (AudioBitrate == 0)
                {
                    return "0";
                }

                if (AudioBitrate>1000000)
                {
                    return (AudioBitrate / 1000000.00).ToString("N2");
                }
                if (AudioBitrate > 1000)
                {
                    return (AudioBitrate / 1000.00).ToString("N2");
                }

                return (AudioBitrate).ToString("N2");
            }
        }

        public string AudioBitRateHRUnit
        {
            get
            {
                if (AudioBitrate > 1000000)
                {
                    return "Mb/s";
                }
                if (AudioBitrate > 1000)
                {
                    return "Kb/s";
                }

                return "b/s";
            }
        }

        private byte _addSamplesOddByte;
        private bool _addSamplesOddByteSet = false;
        private double _power = 0;
        private DateTime _lastPowerCalculation = DateTime.MinValue;

        public IThreadWorkerInfo OFDMThreadStat { get; set; } = null;
        public IThreadWorkerInfo MSCThreadStat { get; set; } = null;
        public IThreadWorkerInfo FICThreadStat { get; set; } = null;
        public IThreadWorkerInfo SFMThreadStat { get; set; } = null;
        public IThreadWorkerInfo AACThreadStat { get; set; } = null;
    }
}
