using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

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
            while (i < iqData.Length / 2)
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
    }
}
