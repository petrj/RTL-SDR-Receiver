using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using RTLSDR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class MainPageViewModel : BaseNotifableObject
    {
        private ILoggingService _loggingService;
        private RTLSDR.RTLSDR _driver;

        private int _freq = 104000000;
        private int _sampleRate = 1000000;

        public MainPageViewModel(ILoggingService loggingService, RTLSDR.RTLSDR driver)
        {
            _driver = driver;
            _loggingService = loggingService;

            _loggingService.Debug("MainPageViewModel");

            WeakReferenceMessenger.Default.Register<NotifyDriverIconChangeMessage>(this, (recipient, msg) =>
            {
                OnPropertyChanged(nameof(DriverIcon));
            });

            Task.Run(() =>
            {
                while (true)
                {
                    OnPropertyChanged(nameof(Bitrate));

                    Thread.Sleep(1000);
                }
            });

            _loggingService.Debug("MainPageViewModel started");
        }

        public int GetScaledSize(int normalSize)
        {
            // TODO: scale font size by configuration
            return normalSize;
        }

        public string FontSizeForLargeCaption
        {
            get
            {
                return GetScaledSize(25).ToString();
            }
        }

        public string FontSizeForCaption
        {
            get
            {
                return GetScaledSize(17).ToString();
            }
        }

        public string FontSizeForLabel
        {
            get
            {
                return GetScaledSize(12).ToString();
            }
        }

        public string DriverIcon
        {
            get
            {
                if (_driver == null || _driver.State != DriverStateEnum.Connected)
                {
                    return "disconnected";
                }

                return "connected";
            }
        }

        public int Frequency
        {
            get
            {
                return _freq;
            }
            set
            {
                _freq = value;

                OnPropertyChanged(nameof(Frequency));
                OnPropertyChanged(nameof(FrequencyWholePartMHz));
                OnPropertyChanged(nameof(FrequencyDecimalPartMHz));
            }
        }

        public string Bitrate
        {
            get
            {
                if (_driver == null)
                    return "";

                if (_driver.Bitrate > 1000000)
                {
                    return (_driver.Bitrate / 1000000).ToString("N0") + " Mb/s";
                } else
                {
                    return (_driver.Bitrate / 1000).ToString("N0") + " Kb/s";
                }
            }
        }

        public int SampleRate
        {
            get
            {
                return _sampleRate;
            }
            set
            {
                _sampleRate = value;

                OnPropertyChanged(nameof(SampleRate));
                OnPropertyChanged(nameof(SampleRateWholePart));
                OnPropertyChanged(nameof(SampleRateDecimalPart));
            }
        }

        public string SampleRateWholePart
        {
            get
            {
                if (_sampleRate >= 1000000)
                {
                    return Convert.ToInt64(Math.Floor(_sampleRate / 1000000.0)).ToString();
                } else
                {
                    return Convert.ToInt64(Math.Floor(_sampleRate / 1000.0)).ToString();
                }
            }
        }

        public string SampleRateDecimalPart
        {
            get
            {
                if (_sampleRate >= 1000000)
                {
                    var part = (_sampleRate / 1000000.0) - Convert.ToInt64(Math.Floor(_sampleRate / 1000000.0));
                    var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                    return $".{part1000} Ms/s";
                } else
                {
                    var part = (_sampleRate / 1000.0) - Convert.ToInt64(Math.Floor(_sampleRate / 1000.0));
                    var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                    return $".{part1000} Ks/s";
                }
            }
        }

        public string FrequencyWholePartMHz
        {
            get
            {
                return Convert.ToInt64(Math.Floor(_freq / 1000000.0)).ToString();
            }
        }

        public string FrequencyDecimalPartMHz
        {
            get
            {
                var part = (_freq / 1000000.0) - Convert.ToInt64(Math.Floor(_freq / 1000000.0));
                var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                return $".{part1000} MHz";
            }
        }
    }
}
