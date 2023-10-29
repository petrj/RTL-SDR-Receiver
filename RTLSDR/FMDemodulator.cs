using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace RTLSDR
{
    public  class FMDemodulator
    {
        public static byte[] DemodulateIQ(byte[] rawData, double sampleRate, double carrierFrequency)
        {
            Complex[] iqData = new Complex[rawData.Length / 2];
            for (int i = 0; i < iqData.Length; i++)
            {
                int iSample = rawData[2 * i] - 128; // Assuming raw data is unsigned byte
                int qSample = rawData[2 * i + 1] - 128;
                iqData[i] = new Complex(iSample, qSample);
            }

            // Demodulation using the sample rate and carrier frequency
            double deltaPhase = 2.0 * Math.PI * carrierFrequency / sampleRate;
            byte[] demodulatedData = new byte[iqData.Length];

            double currentPhase = 0.0;

            for (int i = 0; i < iqData.Length; i++)
            {
                Complex sample = iqData[i];
                double phaseDiff = Math.Atan2(sample.Imaginary, sample.Real);
                currentPhase += deltaPhase;

                byte demodulatedValue = (byte)(phaseDiff / currentPhase * 128);
                demodulatedData[i] = demodulatedValue;
            }

            return demodulatedData;
        }
    }
}
