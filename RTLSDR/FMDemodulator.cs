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

        public short[] FMDemodulate(short[] lp)
        {
            var res = new short[lp.Length / 2];

            res[0] = PolarDiscriminant(lp[0], lp[1], pre_r, pre_j);

            for (var i = 2; i < (lp.Length - 1); i += 2)
            {
                res[i / 2] = PolarDiscriminant(lp[i], lp[i + 1], lp[i - 2], lp[i - 1]);
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
            var downsample = (1000000 / samplerate) + 1;

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
    }
}

