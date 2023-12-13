using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR
{
    public struct DSPComplex
    {
        public DSPComplex(float r, float i)
        {
            Real = r;
            Imag = i;
        }

        public DSPComplex(double r, double i)
        {
            Real = (float)r;
            Imag = (float)i;
        }

        float Real { get; set; }
        float Imag { get; set; }
    }

    public class DABProcessor
    {
        public static DSPComplex[] ToDSPComplex(byte[] iqData, int length)
        {
            var res = new DSPComplex[length];

            for (int i = 0; i < length/2; i++)
            {
                res[i] = new DSPComplex(
                    (iqData[i * 2 + 0] - 128) / 128.0,
                    (iqData[i * 2 + 1] - 128) / 128.0);
            }

            return res;
        }


        public byte[] ProcessData(byte[] IQData, int length)
        {
            var res = new byte[0];

            var complexData = ToDSPComplex(IQData, length);

            return res;
        }
    }
}
