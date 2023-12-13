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

        public float L1Norm()
        {
            return Math.Abs(Real) + Math.Abs(Imag);
        }

        float Real { get; set; }
        float Imag { get; set; }
    }

    public class DABProcessor
    {
        private const int INPUT_RATE = 2048000;
        private const int BANDWIDTH = 1536000;

        public DABProcessor()
        {
            BuildOscillatorTable();
        }

        public DSPComplex[] OscillatorTable { get; set; } = null;

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

        private void BuildOscillatorTable()
        {
            OscillatorTable = new DSPComplex[INPUT_RATE];

            for (int i = 0; i < INPUT_RATE; i++)
            {
                OscillatorTable[i] = new DSPComplex(
                    Math.Cos(2.0 * Math.PI * i / INPUT_RATE),
                    Math.Sin(2.0 * Math.PI * i / INPUT_RATE));
            }

        }

        public byte[] ProcessData(byte[] IQData, int length)
        {
            var res = new byte[0];

            var complexData = ToDSPComplex(IQData, length);

            return res;
        }
    }
}
