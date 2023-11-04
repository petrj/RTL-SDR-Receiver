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
        private int _SDRSampleRate = 2000000;
        private int _FMSampleRate = 48000;

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
                    OnPropertyChanged(nameof(RTLBitrate));
                    OnPropertyChanged(nameof(DemodulationBitrate));

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

        public string RTLBitrate
        {
            get
            {
                if (_driver == null)
                    return "";

                if (_driver.RTLBitrate > 1000000)
                {
                    return (_driver.RTLBitrate / 1000000).ToString("N0") + " Mb/s";
                } else
                {
                    return (_driver.RTLBitrate / 1000).ToString("N0") + " Kb/s";
                }
            }
        }

        public string DemodulationBitrate
        {
            get
            {
                if (_driver == null)
                    return "";

                if (_driver.DemodulationBitrate > 1000000)
                {
                    return (_driver.DemodulationBitrate / 1000000).ToString("N0") + " Mb/s";
                }
                else
                {
                    return (_driver.DemodulationBitrate / 1000).ToString("N0") + " Kb/s";
                }
            }
        }

        public int SDRSampleRate
        {
            get
            {
                return _SDRSampleRate;
            }
            set
            {
                _SDRSampleRate = value;

                OnPropertyChanged(nameof(SDRSampleRate));
                OnPropertyChanged(nameof(SDRSampleRateWholePart));
                OnPropertyChanged(nameof(SDRSampleRateDecimalPart));
            }
        }

        public string SDRSampleRateWholePart
        {
            get
            {
                if (_SDRSampleRate >= 1000000)
                {
                    return Convert.ToInt64(Math.Floor(_SDRSampleRate / 1000000.0)).ToString();
                } else
                {
                    return Convert.ToInt64(Math.Floor(_SDRSampleRate / 1000.0)).ToString();
                }
            }
        }

        public string SDRSampleRateDecimalPart
        {
            get
            {
                if (_SDRSampleRate >= 1000000)
                {
                    var part = (_SDRSampleRate / 1000000.0) - Convert.ToInt64(Math.Floor(_SDRSampleRate / 1000000.0));
                    var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                    return $".{part1000} Ms/s";
                } else
                {
                    var part = (_SDRSampleRate / 1000.0) - Convert.ToInt64(Math.Floor(_SDRSampleRate / 1000.0));
                    var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                    return $".{part1000} Ks/s";
                }
            }
        }

        public int FMSampleRate
        {
            get
            {
                return _FMSampleRate;
            }
            set
            {
                _FMSampleRate = value;

                OnPropertyChanged(nameof(FMSampleRate));
                OnPropertyChanged(nameof(FMSampleRateWholePart));
                OnPropertyChanged(nameof(FMSampleRateDecimalPart));
            }
        }

        public string FMSampleRateWholePart
        {
            get
            {
                if (_FMSampleRate >= 1000000)
                {
                    return Convert.ToInt64(Math.Floor(_FMSampleRate / 1000000.0)).ToString();
                }
                else
                {
                    return Convert.ToInt64(Math.Floor(_FMSampleRate / 1000.0)).ToString();
                }
            }
        }

        public string FMSampleRateDecimalPart
        {
            get
            {
                if (_FMSampleRate >= 1000000)
                {
                    var part = (_FMSampleRate / 1000000.0) - Convert.ToInt64(Math.Floor(_FMSampleRate / 1000000.0));
                    var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                    return $".{part1000} Ms/s";
                }
                else
                {
                    var part = (_FMSampleRate / 1000.0) - Convert.ToInt64(Math.Floor(_FMSampleRate / 1000.0));
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
