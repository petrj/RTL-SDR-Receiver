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

        public int FrequencyKHz
        {
            get
            {
                return _driver.Frequency / 1000;
            }
            set
            {
                //_driver.SetFrequency(value);

                OnPropertyChanged(nameof(FrequencyKHz));
                OnPropertyChanged(nameof(FrequencyWholePartMHz));
                OnPropertyChanged(nameof(FrequencyDecimalPartMHz));
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
            var freqMhz = _driver.Frequency / 1000000.0;
            var roundedFreqMhz10 = Math.Round(freqMhz * 10);

            _driver.Frequency = Convert.ToInt32(roundedFreqMhz10 * 1000000.0 / 10.0);
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

        public string SDRSampleRateKHz
        {
            get
            {
                return Convert.ToInt32(_driver.Settings.SDRSampleRate / 1000) + " KHz";
            }
        }

        public string FMSampleRateKHz
        {
            get
            {
                return Convert.ToInt32(_driver.Settings.FMSampleRate / 1000) + " KHz";
            }
        }

        public string FrequencyWholePartMHz
        {
            get
            {
                return Convert.ToInt64(Math.Floor(_driver.Frequency / 1000000.0)).ToString();
            }
        }

        public string FrequencyDecimalPartMHz
        {
            get
            {
                var part = (_driver.Frequency / 1000000.0) - Convert.ToInt64(Math.Floor(_driver.Frequency / 1000000.0));
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
