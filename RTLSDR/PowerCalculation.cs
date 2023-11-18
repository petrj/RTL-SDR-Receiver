using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RTLSDR
{
    public class PowerCalculation
    {
        private DateTime _lastCalculationTime;
        private double _lastPower;
        private double _maxPower;

        public PowerCalculation()
        {
            _lastCalculationTime = DateTime.MinValue;
            _lastPower = 0;
            _maxPower = PowerCalculation.MaxPower;
        }

        public static double MaxPower
        {
            get { return GetCurrentPower(127, 127); }
        }

        public double GetPowerPercent(byte[] IQData, int bytesRead)
        {
            var now = DateTime.Now;

            var totalSeconds = (now - _lastCalculationTime).TotalSeconds;
            if (totalSeconds > 1)
            {
                if (IQData.Length > 0)
                {
                    _lastPower = GetAvgPower(IQData, bytesRead, 100);
                } else
                {
                    _lastPower = 0;
                }

                _lastCalculationTime = now;
            }

            return _lastPower / (_maxPower / 100);
        }

        public static double GetCurrentPower(int I, int Q)
        {
            if (I == 0 && Q == 0) return 0;

            return 10 * Math.Log(10 * (Math.Pow(I, 2) + Math.Pow(Q, 2)));
        }

        public static double GetAvgPower(byte[] IQData, int bytesRead, int valuesCount = 100)
        {
            // first 100 numbers:

            if (valuesCount > IQData.Length / 2)
            {
                valuesCount = IQData.Length / 2;
            }

            if (valuesCount > bytesRead*2)
            {
                valuesCount = bytesRead * 2;
            }

            double avgPower = 0;

            for (var i = 0; i < valuesCount*2; i = i + 2)
            {
                var power = PowerCalculation.GetCurrentPower(IQData[i + 0] - 127, IQData[i + 1] - 127);

                avgPower += power / (double)valuesCount;
            }

            return avgPower;
        }
    }
}
