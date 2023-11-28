using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace RTLSDR
{
    public class FMDemodulator
    {
        #region rlt_fm

        // https://github.com/osmocom/rtl-sdr/blob/master/src/rtl_fm.c

        short pre_r = 0;
        short pre_j = 0;
        short now_r = 0;
        short now_j = 0;
        int prev_index = 0;

        public static short PolarDiscriminant(int ar, int aj, int br, int bj)
        {
            double angle;

            // multiply
            var cr = ar * br - aj * (-bj);
            var cj = aj * br + ar * (-bj);

            angle = Math.Atan2(cj, cr);
            return (short)(angle / 3.14159 * (1 << 14));
        }

        private static int fast_atan2(int y, int x)
        /* pre scaled for int16 */
        {
            int yabs, angle;
            int pi4 = (1 << 12), pi34 = 3 * (1 << 12);  // note pi = 1<<14
            if (x == 0 && y == 0)
            {
                return 0;
            }
            yabs = y;
            if (yabs < 0)
            {
                yabs = -yabs;
            }
            if (x >= 0)
            {
                angle = pi4 - pi4 * (x - yabs) / (x + yabs);
            }
            else
            {
                angle = pi34 - pi4 * (x + yabs) / (yabs - x);
            }
            if (y < 0)
            {
                return -angle;
            }
            return angle;
        }

        private static short FastPolarDiscriminant(int ar, int aj, int br, int bj)
        {
            var cr = ar * br - aj * (-bj);
            var cj = aj * br + ar * (-bj);

            return Convert.ToInt16(fast_atan2(cj, cr));
        }

        public int FMDemodulate(short[] lp, int count, bool fast = false)
        {
            //var res = new short[lp.Length / 2];

            lp[0] = PolarDiscriminant(lp[0], lp[1], pre_r, pre_j);

            for (var i = 2; i < (count - 2); i += 2)
            {
                if (fast)
                {
                    lp[i / 2] = FastPolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
                }
                else
                {
                    lp[i / 2] = PolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
                }
            }
            pre_r = lp[lp.Length - 2];
            pre_j = lp[lp.Length - 1];

            return count / 2;
        }

        public static short[] Move(byte[] iqData, int count, short vector)
        {
            var buff = new short[count];

            for (int i = 0; i < count; i++)
            {
                buff[i] = (short)(iqData[i] + vector);
            }

            return buff;
        }

        public static void FillBuffer(short[] buffer, byte[] data, int count, short moveVector)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[i] = data[i];
                if (moveVector != 0)
                {
                    buffer[i] += moveVector;
                }
            }
        }

        public int LowPass(short[] iqData, int count, double samplerate)
        {
            var downsample = Convert.ToInt32((1000000 / samplerate) + 1);

            var i = 0;
            var i2 = 0;
            while (i < count - 1)
            {
                now_r += iqData[i + 0];
                now_j += iqData[i + 1];
                i += 2;
                prev_index++;
                if (prev_index < downsample)
                {
                    continue;
                }
                iqData[i2] = now_r;
                iqData[i2 + 1] = now_j;
                prev_index = 0;
                now_r = 0;
                now_j = 0;
                i2 += 2;
            }

            return i2;
        }

        public static byte[] ToByteArray(short[] iqData)
        {
            return ToByteArray(iqData, iqData.Length);
        }

        public static byte[] ToByteArray(short[] iqData, int count)
        {
            var res = new byte[count * 2];

            var pos = 0;
            for (int i = 0; i < count; i++)
            {
                var dataToWrite = BitConverter.GetBytes(iqData[i]);
                res[pos + 0] = (byte)dataToWrite[0];
                res[pos + 1] = (byte)dataToWrite[1];
                pos += 2;
            }

            return res;
        }

        public void DeemphFilter(short[] lp, int count,  int sampleRate = 170000)
        {
            var deemph_a = Convert.ToInt32(1.0 / ((1.0 - Math.Exp(-1.0 / (sampleRate * 75e-6)))));

            var avg = 0;
            for (var i = 0; i < count; i++)
            {
                var d = lp[i] - avg;
                if (d > 0)
                {
                    avg += (d + deemph_a / 2) / deemph_a;
                }
                else
                {
                    avg += (d - deemph_a / 2) / deemph_a;
                }

                lp[i] = Convert.ToInt16(avg);
            }
        }

        public int LowPassReal(short[] lp, int count,  int sampleRateOut = 170000, int sampleRate2 = 32000)
        {
            int now_lpr = 0;
            int prev_lpr_index = 0;

            int i = 0;
            int i2 = 0;

            while (i < count)
            {
                now_lpr += lp[i];
                i++;
                prev_lpr_index += sampleRate2;

                if (prev_lpr_index < sampleRateOut)
                {
                    continue;
                }

                lp[i2] = Convert.ToInt16(now_lpr / ((double)sampleRateOut / (double)sampleRate2));

                prev_lpr_index -= sampleRateOut;
                now_lpr = 0;
                i2 += 1;
            }

            return i2;
        }

        #endregion

        /*
        #region AI

        public static short[] ExtractStereoSignal(short[] IQ, int sampleRate)
        {
            float pilotToneFrequency = 19000f; // Frekvence pilotního tónu pro FM stereo
            int bufferSize = IQ.Length / 2;

            short[] stereoSignal = new short[bufferSize];
            float pilotPhase = 0f;
            float pilotPhaseIncrement = 2f * (float)Math.PI * pilotToneFrequency / sampleRate;

            for (int i = 0; i < bufferSize; i++)
            {
                // Výpočet fáze aktuálního vzorku
                float phase = (float)Math.Atan2(IQ[i*2+1], IQ[i*2+0]);

                // Generování inverzního pilotního tónu
                float inversePilotTone = (float)Math.Sin(pilotPhase);

                // Aktualizace fáze pilotního tónu
                pilotPhase += pilotPhaseIncrement;
                if (pilotPhase > 2 * Math.PI) pilotPhase -= 2 * (float)Math.PI;

                // Modulace vzorku inverzním pilotním tónem pro získání stereo rozdílového signálu
                stereoSignal[i] = Convert.ToInt16(phase * inversePilotTone);
            }

            // Filtrace a další zpracování stereo signálu může být potřebné zde

            return stereoSignal;
        }

        // Metoda pro stereo demodulaci
        public static short[] DemodulateStereo(short[] IQ, int sampleRate)
        {
            var demod = new FMDemodulator();

            var monoSignalLength = demod.FMDemodulate(IQ, false);
            var stereoSignal = ExtractStereoSignal(IQ, sampleRate);

            short[] result = new short[monoSignal.Length*2];

            for (int i = 0; i < monoSignal.Length; i++)
            {
                result[i*2+0] = Convert.ToInt16((monoSignal[i] + stereoSignal[i]) / 2); // L = (Mono + Stereo) / 2
                result[i*2+1] = Convert.ToInt16((monoSignal[i] - stereoSignal[i]) / 2); // R = (Mono - Stereo) / 2
            }

            return result;
        }

        public static short[] DemodulateStereoDeemph(short[] IQ, int inputSampleRate, int outputSampleRate)
        {
            var demod = new FMDemodulator();

            var monoSignal = demod.FMDemodulate(IQ, false);
            var stereoSignal = ExtractStereoSignal(IQ, inputSampleRate);

            var deemphDataMonoSignal = demod.DeemphFilter(monoSignal, inputSampleRate);
            var deemphDataMonoSignalFinal = demod.LowPassReal(deemphDataMonoSignal, inputSampleRate, outputSampleRate);

            var deemphDataStereoSignal = demod.DeemphFilter(stereoSignal, inputSampleRate);
            var deemphDataStereoSignalFinal = demod.LowPassReal(deemphDataStereoSignal, inputSampleRate, outputSampleRate);

            short[] result = new short[deemphDataMonoSignalFinal.Length * 2];

            for (int i = 0; i < deemphDataMonoSignalFinal.Length; i++)
            {
                result[i * 2 + 0] = Convert.ToInt16((deemphDataMonoSignalFinal[i] + deemphDataStereoSignalFinal[i]) / 2); // L = (Mono + Stereo) / 2
                result[i * 2 + 1] = Convert.ToInt16((deemphDataMonoSignalFinal[i] - deemphDataStereoSignalFinal[i]) / 2); // R = (Mono - Stereo) / 2
            }

            return result;
        }

        #endregion
        */
    }
}

