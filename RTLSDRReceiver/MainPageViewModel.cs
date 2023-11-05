using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using RTLSDR;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class MainPageViewModel : BaseNotifableObject
    {
        public ObservableCollection<GainValue> GainValues { get; set; } = new ObservableCollection<GainValue>();
        public ObservableCollection<SampleRateValue> SampleRates { get; set; } = new ObservableCollection<SampleRateValue>();

        private ILoggingService _loggingService;
        private RTLSDR.RTLSDR _driver;

        private int _freq = 104000000;
        private int _SDRSampleRate = 1000000;
        private int _FMSampleRate = 96000;
        private bool _autoGain = true;
        private int _gain = 0;

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
                    OnPropertyChanged(nameof(AmplitudePercent));
                    OnPropertyChanged(nameof(AmplitudePercentProgress));
                    OnPropertyChanged(nameof(AmplitudePercentLabel));

                    Thread.Sleep(1000);
                }
            });

            FillGainValues();
            FillSampleRates();

            _loggingService.Debug("MainPageViewModel started");
        }

        public void FillSampleRates()
        {
            SampleRates.Clear();

            SampleRates.Add(new SampleRateValue(1000000));
            SampleRates.Add(new SampleRateValue(1024000));
            SampleRates.Add(new SampleRateValue(1800000));
            SampleRates.Add(new SampleRateValue(1920000));
            SampleRates.Add(new SampleRateValue(2000000));
            SampleRates.Add(new SampleRateValue(2048000));
            SampleRates.Add(new SampleRateValue(2400000));
        }

        public void FillGainValues()
        {
            GainValues.Clear();

            // adding auto
            GainValues.Add(new GainValue());

            if (_driver == null)
                return;

            var gainArray = new List<int>();

            switch (_driver.TunerType)
            {
                case TunerTypeEnum.RTLSDR_TUNER_UNKNOWN:
                case TunerTypeEnum.RTLSDR_TUNER_R828D:
                case TunerTypeEnum.RTLSDR_TUNER_FC2580:
                    gainArray.Add(0);
                    break;
                case TunerTypeEnum.RTLSDR_TUNER_E4000:
                    gainArray.AddRange(new List<int> { -10, 15, 40, 65, 90, 115, 140, 165, 190, 215, 240, 290, 340, 420 });
                    break;
                case TunerTypeEnum.RTLSDR_TUNER_FC0013:
                    gainArray.AddRange(new List<int> { -99, -73, -65, -63, -60, -58, -54, 58, 61, 63, 65, 67, 68, 70, 71, 179, 181, 182, 184, 186, 188, 191, 197 });
                    break;
                case TunerTypeEnum.RTLSDR_TUNER_R820T:
                    gainArray.AddRange(new List<int> { 0, 9, 14, 27, 37, 77, 87, 125, 144, 157, 166, 197, 207, 229, 254, 280, 297, 328, 338, 364, 372, 386, 402, 421, 434, 439, 445, 480, 496 });
                    break;
            }

            foreach (var i in gainArray)
            {
                GainValues.Add(new GainValue(i));
            }
        }

        private GainValue GetGainValue(int? value)
        {
            foreach (var g in GainValues)
            {
                if (g.Value == value)
                {
                    return g;
                }
            }

            return null;
        }

        private SampleRateValue GetSampleRateValue(int value)
        {
            foreach (var s in SampleRates)
            {
                if (s.Value == value)
                {
                    return s;
                }
            }

            return null;
        }

        public GainValue GainValue
        {
            get
            {
                if (_autoGain)
                {
                    return GetGainValue(null);
                }

                return GetGainValue(_gain);
            }
            set
            {
                if (value != null && value.Value.HasValue)
                {
                    _gain = value.Value.Value;
                    _autoGain = false;
                } else
                {
                    _autoGain = true;
                    _gain = 0;
                }

                _driver.SetGainMode(!_autoGain);
                if (!_autoGain)
                {
                    _driver.SetGain(_gain);
                }

                if (_driver.TunerType == TunerTypeEnum.RTLSDR_TUNER_E4000)
                {
                    _driver.SetIfGain(!_autoGain);
                }

                OnPropertyChanged(nameof(GainValue));
            }
        }

        public SampleRateValue SampleRateValue
        {
            get
            {
                return GetSampleRateValue(_SDRSampleRate);
            }
            set
            {
                SDRSampleRate = value.Value;

                _driver.SetSampleRate(_SDRSampleRate);
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

        public double AmplitudePercentProgress
        {
            get
            {
                return AmplitudePercent / 100;
            }
        }

        public string AmplitudePercentLabel
        {
            get
            {
                return $"{AmplitudePercent.ToString("N0")} %";
            }
        }

        public double AmplitudePercent
        {
            get
            {
                if (_driver == null)
                    return 0;

                return _driver.AmplitudePercent;
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
