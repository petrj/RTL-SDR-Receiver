using System;

namespace RTLSDR.Common
{
    public interface IDemodulator
    {
        int Samplerate { get; set; }
        void AddSamples(byte[] IQData, int length);
        void Finish();
        double PercentSignalPower { get; }

        event EventHandler OnDemodulated;
        event EventHandler OnFinished;
        event EventHandler OnServiceFound;
    }
}
