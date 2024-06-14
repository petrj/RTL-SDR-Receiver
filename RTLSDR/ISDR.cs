using LoggerService;
using System;
using System.Collections.Generic;
using System.Runtime;
using System.Text;

namespace RTLSDR
{
    public interface ISDR
    {
        string DeviceName { get; }
        int Frequency { get; }
        TunerTypeEnum TunerType { get; }
        long RTLBitrate { get; }
        long DemodulationBitrate { get; }
        double PowerPercent { get; }
        double Power { get; }
        void Init(DriverInitializationResult driverInitializationResult);
        void Connect();
        void Disconnect();
        void SendCommand(Command command);
        void SetFrequency(int freq);
        void SetFrequencyCorrection(int correction);
        void SetSampleRate(int sampleRate);
        void SetDirectSampling(int value);
        void SetGainMode(bool manual);
        void SetGain(int gain);
        void SetIfGain(bool ifGain);
        void SetAGCMode(bool automatic);
    }
}
