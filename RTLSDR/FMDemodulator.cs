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

        public short[] FMDemodulate(short[] lp, bool fast = false)
        {
            var res = new short[lp.Length / 2];

            res[0] = PolarDiscriminant(lp[0], lp[1], pre_r, pre_j);

            for (var i = 2; i < (lp.Length - 1); i += 2)
            {
                if (fast)
                {
                    res[i / 2] = FastPolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
                }
                else
                {
                    res[i / 2] = PolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
                }
            }
            pre_r = lp[lp.Length - 2];
            pre_j = lp[lp.Length - 1];

            return res;
        }

        public static short[] Move(byte[] iqData, int count, double vector)
        {
            var buff = new short[count];

            for (int i = 0; i < count; i++)
            {
                buff[i] = (short)(iqData[i] + vector);
            }

            return buff;
        }

        public static byte[] ToByteArray(short[] iqData)
        {
            var res = new byte[iqData.Length * 2];

            var pos = 0;
            foreach (var data in iqData)
            {
                var dataToWrite = BitConverter.GetBytes(data);
                res[pos + 0] = (byte)dataToWrite[0];
                res[pos + 1] = (byte)dataToWrite[1];
                pos += 2;
            }

            return res;
        }

        public short[] LowPass(short[] iqData, double samplerate)
        {
            var downsample = Convert.ToInt32((1000000 / samplerate) + 1);

            var i = 0;
            var i2 = 0;
            while (i < iqData.Length-1)
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

            var res = new short[i2];
            Array.Copy(iqData, res, i2);

            return res;
        }

        public short[] DeemphFilter(short[] lp, int sampleRate = 170000)
        {
            var deemph_a = Convert.ToInt32(1.0 / ((1.0 - Math.Exp(-1.0 / (sampleRate * 75e-6)))));

            var res = new short[lp.Length];
            var avg = 0;
            for (var i = 0; i < lp.Length; i++)
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

                res[i] = Convert.ToInt16(avg);
            }

            return  res;
        }

        public short[] LowPassReal(short[] lp, int sampleRateOut = 170000, int sampleRate2 = 32000)
        {
            int now_lpr = 0;
            int prev_lpr_index = 0;

            int i = 0;
            int i2 = 0;

            while (i < lp.Length)
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

            var res = new short[i2];
            Array.Copy(lp, res, i2);

            return res;
        }

        #endregion

        #region rtl_fm stereo patch

        // https://www.abclinuxu.cz/blog/mirek/2013/9/hratky-kolem-sdr-rtl-fm-stereo

        public static byte[] Rotate90(byte[] buf, int len)
        /* 90 rotation is 1+0j, 0+1j, -1+0j, 0-1j
           or [0, 1, -3, 2, -4, -5, 7, -6] */
        {
            int i;
            byte tmp;
            for (i = 0; i < len-8; i += 8)
            {
                /* uint8_t negation = 255 - x */
                tmp = (byte)(255 - buf[i + 3]);
                buf[i + 3] = buf[i + 2];
                buf[i + 2] = tmp;

                buf[i + 4] = (byte)(255 - buf[i + 4]);
                buf[i + 5] = (byte)(255 - buf[i + 5]);

                tmp = (byte)(255 - buf[i + 6]);
                buf[i + 6] = buf[i + 7];
                buf[i + 7] = tmp;
            }

            var res = new byte[len];
            Array.Copy(buf, res, len);

            return res;
        }

        public struct LowPassedComplexData
        {
            public int size;  /* for SSE size must be multiple of 8 */
            public int[] br;
            public int[] bi;
            public int[] fc;
            public int pos;
            public int sum;
        }

        public static LowPassedComplexData BuildLowPassComplex(double freq = 96000, double samplerate = 192000, int lpcSize = 32)
        {
            //  "LP Complex: FIR hamming

            var res = new LowPassedComplexData();
            res.size = lpcSize;

            int i, j;
            double ft, fv, fi;

            var downsample = Convert.ToInt32((1000000 / samplerate) + 1);
            ft = freq / (double)(downsample * samplerate);

            res.br = new int[res.size << 1];
            res.bi = new int[res.size << 1];
            res.fc = new int[res.size << 1];

            res.pos = 0;
            for (i = 0; i < res.size; i++)
            {
                res.br[i] = 0;
                res.bi[i] = 0;
                fi = (double)i - ((double)(res.size - 1) / 2.0);
                /* low pass */
                fv = (fi == 0) ? 2.0 * ft : Math.Sin(2.0 * Math.PI * ft * fi) / (Math.PI * fi);
                /* hamming window */
                fv *= (0.54 - 0.46 * Math.Cos(2.0 * Math.PI * (double)i / (double)(res.size - 1)));
                /* convert to int16, always below 1 */
                res.fc[i] = Convert.ToInt32(fv * 32768.0);
            }
            res.sum = 32768;

            return res;
        }

        public struct LowPassedRealData
        {
            public int size;  /* for SSE size must be multiple of 8 */

            public int swf;
            public int cwf;
            public int pp;

            public int pos;
            public int sum;

            public int[] br;
            public int[] bm;
            public int[] bs;
            public int[] fm;
            public int[] fp;
            public int[] fs;
        }

        public static LowPassedRealData BuildLowPassReal(double freq = 96000, double samplerate = 192000, int lpcSize = 32)
        {
            int i, j;
            double fmh, fpl, fph, fsl, fsh, fv, fi, fh, wf;

            var res = new LowPassedRealData();
            res.size = lpcSize;

            // LP Real: FIR hamming stereo

            //fm->stereo = 1;

            wf = 2.0 * Math.PI * 19000.0 / samplerate;
            res.swf = Convert.ToInt32(32767.0 * Math.Sin(wf));
            res.cwf = Convert.ToInt32(32767.0 * Math.Cos(wf));
            res.pp = 0;
            fmh = (double)freq / (double)samplerate;
            fpl = 18000.0 / (double)samplerate;
            fph = 20000.0 / (double)samplerate;
            fsl = 21000.0 / (double)samplerate;
            fsh = 55000.0 / (double)samplerate;
            res.br = new int[res.size << 1];
            res.bm = new int[res.size << 1];
            res.bs = new int[res.size << 1];
            res.fm = new int[res.size << 1];
            res.fp = new int[res.size << 1];
            res.fs = new int[res.size << 1];
            res.pos = 0;

            for (i = 0; i < res.size; i++)
            {
                res.br[i] = 0;
                res.bm[i] = 0;
                res.bs[i] = 0;
                fi = (double)i - ((double)(res.size - 1) / 2.0);
                /* hamming window */
                fh = (0.54 - 0.46 * Math.Cos(2.0 * Math.PI * (double)i / (double)(res.size - 1)));
                /* low pass */
                fv = (fi == 0) ? 2.0 * fmh : Math.Sin(2.0 * Math.PI * fmh * fi) / (Math.PI * fi);
                res.fm[i] = Convert.ToInt32(fv * fh * 32768.0);
                /* pilot band pass */
                fv = (fi == 0) ? 2.0 * (fph - fpl) : (Math.Sin(2.0 * Math.PI * fph * fi) - Math.Sin(2.0 * Math.PI * fpl * fi)) / (Math.PI * fi);
                res.fp[i] = Convert.ToInt32(fv * fh * 32768.0);
                /* stereo band pass */
                fv = (fi == 0) ? 2.0 * (fsh - fsl) : (Math.Sin(2.0 * Math.PI * fsh * fi) - Math.Sin(2.0 * Math.PI * fsl * fi)) / (Math.PI * fi);
                res.fs[i] = Convert.ToInt32(fv * fh * 32768.0);
            }
            res.sum = 32768;

            return res;
        }

        public void LowPassComplex(byte[] buf, LowPassedComplexData lpc, double samplerate = 192000, double output_scale = 1)
        {
            /* Slow HQ FIR complex filter */

            var len = buf.Length;

            var downsample = Convert.ToInt32((1000000 / samplerate) + 1);

            var res = new short[len/2];

            int i = 0, i2 = 0, i3 = 0;

            while (i < (int)len)
            {
                lpc.br[lpc.pos] = buf[i] - 128;
                lpc.bi[lpc.pos] = buf[i + 1] - 128;
                lpc.pos++;
                i += 2;
                if (++prev_index < downsample) continue;

                for (i3 = 0; i3 < lpc.size; i3++)
                {
                    now_r += Convert.ToInt16((lpc.br[i3] * lpc.fc[i3]));
                    now_j += Convert.ToInt16((lpc.bi[i3] * lpc.fc[i3]));
                }
                res[i2] = (short)((now_r * output_scale) / lpc.sum);
                res[i2 + 1] = (short)((now_j * output_scale) / lpc.sum);
                prev_index = 0;
                now_r = 0;
                now_j = 0;
                i2 += 2;
                /* shift buffers, we can skip few samples at begining, but not big deal */
                if (lpc.pos + downsample >= lpc.size)
                {
                    lpc.pos = lpc.size - downsample;

                    for (var j = 0; j< lpc.pos << 1; j++)
                    {
                        lpc.br[j] = lpc.br[downsample+j];
                        lpc.bi[j] = lpc.bi[downsample + j];
                    }
                }
            }
        }

        #endregion

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

            var monoSignal = demod.FMDemodulate(IQ, false);
            var stereoSignal = ExtractStereoSignal(IQ, sampleRate);

            short[] result = new short[monoSignal.Length*2];

            for (int i = 0; i < monoSignal.Length; i++)
            {
                result[i*2+0] = Convert.ToInt16((monoSignal[i] + stereoSignal[i]) / 2); // L = (Mono + Stereo) / 2
                result[i*2+1] = Convert.ToInt16((monoSignal[i] - stereoSignal[i]) / 2); // R = (Mono - Stereo) / 2
            }

            return result;
        }

        #endregion
    }
}

