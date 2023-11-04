using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace RTLSDR
{
    public class AmpCalculation
    {
        private DateTime _lastCalculationTime;
        private double _lastAmplitude;
        private double _ampMax;

        public AmpCalculation()
        {
            _lastCalculationTime = DateTime.MinValue;
            _lastAmplitude = 0;
            _ampMax = Math.Sqrt(Math.Pow(128, 2) + Math.Pow(128, 2));
        }

        public double GetAmpPercent(byte[] IQData, int valuesCount = 1000)
        {
            var now = DateTime.Now;

            var totalSeconds = (now - _lastCalculationTime).TotalSeconds;
            if (totalSeconds > 1)
            {
                if (IQData.Length > 0)
                {
                    _lastAmplitude = GetAvgAmplitude(IQData, valuesCount);
                } else
                {
                    _lastAmplitude = 0;
                }

                _lastCalculationTime = now;
            }

            return _lastAmplitude / (_ampMax/100);
        }

        /// <summary>
        /// Computing peek amplitude
        ///  (I²+Q²)½
        /// </summary>
        /// <param name="I"></param>
        /// <param name="Q"></param>
        /// <returns></returns>
        public static double GetAmplitude(int I, int Q)
        {
            if (I == 0 && Q == 0) return 0;
            return Math.Sqrt(I * I + Q * Q);
        }

        public static double GetAvgAmplitude(byte[] IQData, int valuesCount = 1000)
        {
            if (valuesCount > IQData.Length / 2)
            {
                valuesCount = IQData.Length / 2;
            }

            double avgmp = 0;

            for (var i = 0; i < valuesCount; i++)
            {
                var amp = AmpCalculation.GetAmplitude(IQData[i * 2 + 0] - 127, IQData[i * 2 + 1] - 127);

                avgmp += amp / (double)valuesCount;
            }

            return avgmp;
        }

        /// <summary>
        /// Computing phase angle
        /// ϕ = tan⁻¹(Q/I)
        /// </summary>
        /// <returns></returns>
        public static double GetPhaseAngle(int I, int Q)
        {
            if (I == 0)
                return 0;

            return Math.Atan(Q/I);
        }
    }
}
