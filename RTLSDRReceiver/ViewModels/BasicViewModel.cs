using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using RTLSDR;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver.ViewModels
{
    public class BasicViewModel : BaseNotifableObject
    {
        protected ILoggingService _loggingService;
        protected RTLSDR.RTLSDR _driver;
        protected IDialogService _dialogService;

        protected int _freq = 104000000;
        protected int _SDRSampleRate = 1056000;
        protected int _FMSampleRate = 96000;
        protected bool _autoGain = true;
        protected int _gain = 37;
        protected bool _deEmphasis = false;
        protected bool _fastAtan = false;

        public BasicViewModel(ILoggingService loggingService, RTLSDR.RTLSDR driver, IDialogService dialogService)
        {
            _driver = driver;
            _loggingService = loggingService;
            _dialogService = dialogService;

            _loggingService.Debug("BasicViewModel");

            WeakReferenceMessenger.Default.Register<NotifyStateChangeMessage>(this, (recipient, msg) =>
            {
                OnPropertyChanged(nameof(DriverIcon));
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsNotConnected));

                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(IsNotRecording));
                OnPropertyChanged(nameof(RecordIcon));
            });
        }

        public double FrequencyKHz
        {
            get
            {
                return _freq / 1000.0;
            }
            set
            {
                _freq = Convert.ToInt32(value * 1000);

                OnPropertyChanged(nameof(FrequencyKHz));
                OnPropertyChanged(nameof(Frequency));
                OnPropertyChanged(nameof(FrequencyWholePartMHz));
                OnPropertyChanged(nameof(FrequencyDecimalPartMHz));
            }
        }

        public bool AutoGain
        {
            get
            {
                return _autoGain;

            }
            set
            {
                _autoGain = value;

                OnPropertyChanged(nameof(AutoGain));
            }
        }

        public int Gain
        {
            get
            {
                return _gain;

            }
            set
            {
                _gain = value;

                OnPropertyChanged(nameof(Gain));
            }
        }

        public double MaxFrequencyKHz
        {
            get
            {
                return 108000.0;
            }
        }

        public double MinFrequencyKHz
        {
            get
            {
                return 87000.5;
            }
        }

        /// <summary>
        /// rounding to tenth
        /// </summary>
        public void RoundFreq()
        {
            var freqMhz = Frequency / 1000000.0;
            var roundedFreqMhz10 = Math.Round(freqMhz * 10);

            Frequency = Convert.ToInt32(roundedFreqMhz10 * 1000000.0 / 10.0);
        }

        public bool DeEmphasis
        {
            get
            {
                return _deEmphasis;

            }
            set
            {
                _deEmphasis = value;

                OnPropertyChanged(nameof(DeEmphasis));
            }
        }

        public bool FastAtan
        {
            get
            {
                return _fastAtan;

            }
            set
            {
                _fastAtan = value;

                OnPropertyChanged(nameof(FastAtan));
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
                OnPropertyChanged(nameof(FrequencyKHz));
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
                }
                else
                {
                    return (_driver.RTLBitrate / 1000).ToString("N0") + " Kb/s";
                }
            }
        }

        public double PowerPercentProgress
        {
            get
            {
                return PowerPercent / 100;
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
                OnPropertyChanged(nameof(SDRSampleRateKHz));
                OnPropertyChanged(nameof(SDRSampleRateWholePart));
                OnPropertyChanged(nameof(SDRSampleRateDecimalPart));
            }
        }

        public string SDRSampleRateKHz
        {
            get
            {
                return Convert.ToInt32(_SDRSampleRate / 1000) + " KHz";
            }
        }

        public string SDRSampleRateWholePart
        {
            get
            {
                if (_SDRSampleRate >= 1000000)
                {
                    return Convert.ToInt64(Math.Floor(_SDRSampleRate / 1000000.0)).ToString();
                }
                else
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
                }
                else
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
                _driver.Settings.FMSampleRate = _FMSampleRate;

                OnPropertyChanged(nameof(FMSampleRate));
                OnPropertyChanged(nameof(FMSampleRateKHz));
                OnPropertyChanged(nameof(FMSampleRateWholePart));
                OnPropertyChanged(nameof(FMSampleRateDecimalPart));
            }
        }

        public string FMSampleRateKHz
        {
            get
            {
                return Convert.ToInt32(_FMSampleRate / 1000) + " KHz";
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

        public string RecordIcon
        {
            get
            {
                if (_driver == null || !_driver.Recording)
                {
                    return "record";
                }

                return "stoprecord";
            }
        }

        public bool IsConnected
        {
            get
            {
                if (_driver == null)
                {
                    return false;
                }

                return _driver.State == DriverStateEnum.Connected;
            }
        }

        public bool IsRecording
        {
            get
            {
                if (_driver == null || _driver.State != DriverStateEnum.Connected)
                {
                    return false;
                }

                return _driver.Recording;
            }
        }

        public bool IsNotRecording
        {
            get
            {
                return !IsRecording;
            }
        }

        public bool IsNotConnected
        {
            get
            {
                if (_driver == null)
                {
                    return false;
                }

                return _driver.State != DriverStateEnum.Connected;
            }
        }


        public string PowerPercentLabel
        {
            get
            {
                return $"{PowerPercent.ToString("N0")} %";
            }
        }

        public double PowerPercent
        {
            get
            {
                if (_driver == null)
                    return 0;

                return _driver.PowerPercent;
            }
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
    }
}
