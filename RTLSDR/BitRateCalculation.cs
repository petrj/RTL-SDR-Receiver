using LoggerService;
using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR
{
    public class BitRateCalculation
    {
        ILoggingService _loggingService;
        private DateTime _lastSpeedCalculationTime;
        private int _bytesReadFromLastSpeedCalculationTime;
        private double _bitRate;
        private string _description;

        public BitRateCalculation(ILoggingService loggingService, string description)
        {
            _loggingService = loggingService;

            _bytesReadFromLastSpeedCalculationTime = 0;
            _lastSpeedCalculationTime = DateTime.Now;
            _bitRate = 0;
            _description = description;
        }

        public double GetBitRate(int bytesRead)
        {
            var now = DateTime.Now;

            var totalSeconds = (now - _lastSpeedCalculationTime).TotalSeconds;
            if (totalSeconds > 1)
            {
                _bitRate = _bytesReadFromLastSpeedCalculationTime * 8 / totalSeconds;

                if (_bitRate > 1000000)
                {
                    _loggingService.Debug($"Bitrate ({_description}): {(_bitRate / 1000000).ToString("N0").PadLeft(20)}  Mb/s");
                }
                else
                {
                    _loggingService.Debug($"Bitrate ({_description}): {(_bitRate / 1000).ToString("N0").PadLeft(20)}  Kb/s");
                }

                _lastSpeedCalculationTime = now;
                _bytesReadFromLastSpeedCalculationTime = 0;
            } else
            {
                _bytesReadFromLastSpeedCalculationTime += bytesRead;
            }

            return _bitRate;
        }
    }
}
