using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace RTLSDR
{
    public  class FMDemodulator
    {
        // https://github.com/osmocom/rtl-sdr/blob/master/src/rtl_fm.c

        public static short PolarDiscriminant(int ar, int aj, int br, int bj)
        {
            double angle;

            // multiply
            var cr = ar * br - aj * (-bj);
            var cj = aj * br + ar * (-bj);

            angle = Math.Atan2(cj, cr);
            return (short)(angle / 3.14159 * (1 << 14));
        }

        public static short[] FMDemodulate(short[] lp)
        {
            var res = new short[lp.Length / 2];

            short pre_r = 0;
            short pre_j = 0;

            res[0] = PolarDiscriminant(lp[0], lp[1], pre_r, pre_j);

            for (var i = 2; i < (lp.Length - 1); i += 2)
            {
                res[i / 2] = PolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
            }
            //pre_r = lp[lp_len - 2];
            //pre_j = lp[lp_len - 1];

            return res;
        }

        public static short[] LowPass(byte[] iqData, double samplerate)
        {
            var downsample = (1000000 / samplerate) + 1;

            var buff = new short[iqData.Length];

            for (int z = 0; z < iqData.Length; z++)
            {
                buff[z] = (short)(iqData[z] - 127);
            }

            short now_r = 0;
            short now_j = 0;
            int prev_index = 0;
            var i = 0;
            var i2 = 0;
            while (i < iqData.Length / 2)
            {
                now_r += buff[i + 0];
                now_j += buff[i + 1];
                i += 2;
                prev_index++;
                if (prev_index < downsample)
                {
                    continue;
                }
                buff[i2] = now_r;
                buff[i2 + 1] = now_j;
                prev_index = 0;
                now_r = 0;
                now_j = 0;
                i2 += 2;
            }

            var res = new short[i2];
            Array.Copy(buff, res, i2);

            return res;
        }

        public double[] Demodulate(byte[] iqData, double sampleRate)
        {
            var iData = new double[iqData.Length / 2];
            var qData = new double[iqData.Length / 2];

            for (int i = 0; i < iqData.Length / 2; i++)
            {
                iData[i] = iqData[i*2+0] - 127.5;
                qData[i] = iqData[i+2+1] - 127.5;
            }

            return Demodulate(iData, qData, sampleRate);
        }

        public double[] Demodulate(double[] iData, double[] qData, double sampleRate)
        {
            // ChatGPT code

            double previousPhase = 0;

            int length = iData.Length;
            double[] audioData = new double[length];

            for (int i = 0; i < length; i++)
            {
                double iSample = iData[i];
                double qSample = qData[i];

                // Calculate the instantaneous phase and frequency
                double phase = Math.Atan2(qSample, iSample);
                double phaseDifference = phase - previousPhase;
                previousPhase = phase;

                // Normalize the phase difference
                phaseDifference = NormalizePhaseDifference(phaseDifference);

                // Calculate the instantaneous frequency deviation
                audioData[i] = phaseDifference * (sampleRate / (2 * Math.PI));
            }

            return audioData;
        }

        private double NormalizePhaseDifference(double phaseDifference)
        {
            phaseDifference %= 2 * Math.PI;
            if (phaseDifference > Math.PI)
            {
                phaseDifference -= 2 * Math.PI;
            }
            else if (phaseDifference < -Math.PI)
            {
                phaseDifference += 2 * Math.PI;
            }

            return phaseDifference;
        }

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
