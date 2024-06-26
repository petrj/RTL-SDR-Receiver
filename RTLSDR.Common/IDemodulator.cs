﻿using System;

namespace RTLSDR.Common
{
    public interface IDemodulator
    {
        int Samplerate { get; set; }
        void AddSamples(byte[] IQData, int length);
        event EventHandler OnDemodulated;
        event EventHandler OnFinished;
        void Finish();
        double PercentSignalPower { get; }
    }
}
