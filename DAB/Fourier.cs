using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    /*
        FFT/DFT algorithm based on Accord-NET (https://github.com/Azure/Accord-NET/blob/master/Sources/Accord.Math/Transforms/FourierTransform2.cs)
        - some optimalizations made by ChatGPT
    */

    public class Fourier
    {
        public static double TotalFFTTimeMs { get; set; } = 0;
        public static double TotalDFTTimeMs { get; set; } = 0;

        public static void FFTBackward(FComplex[] data)
        {
            var startTime = DateTime.Now;

            int n = data.Length, m = Log2(n), tn = 1, tm, odd;
            ReorderData(data);
            for (int k = 1; k <= m; k++)
            {
                var rotation = GetComplexRotation(k);
                tm = tn; tn <<= 1;
                for (int i = 0; i < tm; i++)
                {
                    var t = rotation[i];
                    float tReal = t.Real, tImaginary = t.Imaginary;
                    for (int even = i; even < n; even += tn)
                    {
                        odd = even + tm;
                        var dEven = data[even]; var dOdd = data[odd];
                        float cer = dEven.Real, cei = dEven.Imaginary;
                        float cor = dOdd.Real, coi = dOdd.Imaginary;
                        float tr = cor * tReal - coi * tImaginary;
                        float ti = cor * tImaginary + coi * tReal;
                        dEven.Real += tr;
                        dEven.Imaginary += ti;
                        dOdd.Real = cer - tr;
                        dOdd.Imaginary = cei - ti;
                    }
                }
            }

            TotalFFTTimeMs += (DateTime.Now - startTime).TotalMilliseconds;
        }

        // Reorder data for FFT using
        private static void ReorderData(FComplex[] data)
        {
            var minLength = 2;
            var maxLength = 16384;

            var len = data.Length;

            // check data length
            if ((len < minLength) || (len > maxLength) || (!IsPowerOf2(len)))
                throw new ArgumentException("Incorrect data length.");

            int[] rBits = GetReversedBits(Log2(len));

            for (int i = 0; i < len; i++)
            {
                int s = rBits[i];

                if (s > i)
                {
                    FComplex t = data[i];
                    data[i] = data[s];
                    data[s] = t;
                }
            }
        }

        /// <summary>
        /// One dimensional Discrete Backward Fourier Transform.
        /// </summary>
        /// <param name="data">Data to transform.</param>
        public static FComplex[] DFTBackward(FComplex[] data, double[] cosTable, double[] sinTable)
        {
            var startTime = DateTime.Now;

            int n = data.Length;
            var dst = new FComplex[n];

            Parallel.For(0, n, i =>
            {
                double re = 0;
                double im = 0;

                double cos_i = cosTable[i];
                double sin_i = sinTable[i];

                for (int j = 0; j < n; j++)
                {
                    double cos = cos_i;
                    double sin = sin_i;

                    re += data[j].Real * cos - data[j].Imaginary * sin;
                    im += data[j].Real * sin + data[j].Imaginary * cos;

                    double cos_temp = cos_i * cosTable[i] - sin_i * sinTable[i];
                    sin_i = cos_i * sinTable[i] + sin_i * cosTable[i];
                    cos_i = cos_temp;
                }

                dst[i] = new FComplex(re, im);
            });

            TotalDFTTimeMs += (DateTime.Now - startTime).TotalMilliseconds;

            return dst;
        }

        private static int minBits = 1;
        private static int maxBits = 14;
        private static int[][] reversedBits = new int[maxBits][];
        private static FComplex[,][] complexRotation = new FComplex[maxBits, 2][];

        // Get rotation of complex number
        private static FComplex[] GetComplexRotation(int numberOfBits)
        {
            int directionIndex = 1;

            // Check if the array is already calculated
            if (complexRotation[numberOfBits - 1, directionIndex] == null)
            {
                int n = 1 << (numberOfBits - 1);
                double uR = 1.0;
                double uI = 0.0;
                double angle = -1 * System.Math.PI / n;
                double wR = System.Math.Cos(angle);
                double wI = System.Math.Sin(angle);
                double t;

                FComplex[] rotation = new FComplex[n];

                for (int i = 0; i < n; i++)
                {
                    rotation[i] = new FComplex(uR, uI);
                    t = uR * wI + uI * wR;
                    uR = uR * wR - uI * wI;
                    uI = t;
                }

                // Lazy initialization - add to cache only if not already present
                if (complexRotation[numberOfBits - 1, directionIndex] == null)
                {
                    complexRotation[numberOfBits - 1, directionIndex] = rotation;
                }
            }

            return complexRotation[numberOfBits - 1, directionIndex];
        }

        // Get array, indicating which data members should be swapped before FFT
        private static int[] GetReversedBits(int numberOfBits)
        {
            // Lazy initialization - Check if the array is already calculated
            if (reversedBits[numberOfBits - 1] == null)
            {
                int n = Pow2(numberOfBits);
                int[] rBits = new int[n];

                // Calculate the array using bitwise operations
                for (int i = 0; i < n; i++)
                {
                    int oldBits = i;
                    int newBits = 0;

                    for (int j = 0; j < numberOfBits; j++)
                    {
                        newBits = (newBits << 1) | (oldBits & 1);
                        oldBits >>= 1;  // Použití bitového posunu místo aritmetického
                    }

                    rBits[i] = newBits;
                }

                // Cachování hodnot
                reversedBits[numberOfBits - 1] = rBits;
            }

            return reversedBits[numberOfBits - 1];
        }

        public static int Pow2(int power)
        {
            return ((power >= 0) && (power <= 30)) ? (1 << power) : 0;
        }

        public static bool IsPowerOf2(int x)
        {
            return (x > 0) ? ((x & (x - 1)) == 0) : false;
        }

        /// <summary>
        /// Get base of binary logarithm.
        /// </summary>
        /// <param name="x">Source integer number.</param>
        /// <returns>Power of the number (base of binary logarithm).</returns>
        public static int Log2(int x)
        {
            if (x <= 65536)
            {
                if (x <= 256)
                {
                    if (x <= 16)
                    {
                        if (x <= 4)
                        {
                            if (x <= 2)
                            {
                                if (x <= 1)
                                    return 0;
                                return 1;
                            }
                            return 2;
                        }
                        if (x <= 8)
                            return 3;
                        return 4;
                    }
                    if (x <= 64)
                    {
                        if (x <= 32)
                            return 5;
                        return 6;
                    }
                    if (x <= 128)
                        return 7;
                    return 8;
                }
                if (x <= 4096)
                {
                    if (x <= 1024)
                    {
                        if (x <= 512)
                            return 9;
                        return 10;
                    }
                    if (x <= 2048)
                        return 11;
                    return 12;
                }
                if (x <= 16384)
                {
                    if (x <= 8192)
                        return 13;
                    return 14;
                }
                if (x <= 32768)
                    return 15;
                return 16;
            }

            if (x <= 16777216)
            {
                if (x <= 1048576)
                {
                    if (x <= 262144)
                    {
                        if (x <= 131072)
                            return 17;
                        return 18;
                    }
                    if (x <= 524288)
                        return 19;
                    return 20;
                }
                if (x <= 4194304)
                {
                    if (x <= 2097152)
                        return 21;
                    return 22;
                }
                if (x <= 8388608)
                    return 23;
                return 24;
            }
            if (x <= 268435456)
            {
                if (x <= 67108864)
                {
                    if (x <= 33554432)
                        return 25;
                    return 26;
                }
                if (x <= 134217728)
                    return 27;
                return 28;
            }
            if (x <= 1073741824)
            {
                if (x <= 536870912)
                    return 29;
                return 30;
            }
            return 31;
        }
    }
}
