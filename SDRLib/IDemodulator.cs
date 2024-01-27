using System;
namespace SDRLib
{
    public interface IDemodulator
    {
        int Samplerate { get; set; }
        void AddSamples(byte[] IQData, int length);
        event EventHandler OnDemodulated;
        void Finish();
    }
}
