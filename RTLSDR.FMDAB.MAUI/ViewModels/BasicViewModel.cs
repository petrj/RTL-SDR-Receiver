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
        protected ISDR _driver;
        protected IDialogService _dialogService;
        protected IAppSettings _appSettings;

        public BasicViewModel(ILoggingService loggingService, ISDR driver, IDialogService dialogService, IAppSettings appSettings)
        {
            _driver = driver;
            _loggingService = loggingService;
            _dialogService = dialogService;
            _appSettings = appSettings;

            _loggingService.Debug("BasicViewModel");

            WeakReferenceMessenger.Default.Register<NotifyStateChangeMessage>(this, (recipient, msg) =>
            {
                NotifyStateOrConfigurationChange();
            });
        }

        public void NotifyStateOrConfigurationChange()
        {
            OnPropertyChanged(nameof(DriverIcon));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsNotConnected));

            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsNotRecording));
            OnPropertyChanged(nameof(RecordIcon));

            OnPropertyChanged(nameof(FrequencyKHz));
            OnPropertyChanged(nameof(FrequencyKHzHr));

            OnPropertyChanged(nameof(DriverSampleRateKHz));
            OnPropertyChanged(nameof(DriverSampleRateKHzHr));

            OnPropertyChanged(nameof(AudioSampleRateKHz));
            OnPropertyChanged(nameof(AudioSampleRateKHzHr));
        }

        public int FrequencyKHz
        {
            get
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.DAB:
                        return _appSettings.DABFrequencyKHz;
                    default:
                        return _appSettings.FMFrequencyKHz;
                }
            }
            set
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.DAB:
                        _appSettings.DABFrequencyKHz = value;
                        break;
                    default:
                        _appSettings.FMFrequencyKHz = value;
                        break;
                }

                OnPropertyChanged(nameof(FrequencyKHz));
                OnPropertyChanged(nameof(FrequencyKHzHr));
            }
        }

        public int DriverSampleRateKHz
        {
            get
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.DAB:
                        return _appSettings.DABDriverSampleRate;
                    default:
                        return _appSettings.FMDriverSampleRate;
                }
            }
            set
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.DAB:
                        _appSettings.DABDriverSampleRate = value;
                        break;
                    default:
                        _appSettings.FMDriverSampleRate = value;
                        break;
                }

                OnPropertyChanged(nameof(DriverSampleRateKHz));
                OnPropertyChanged(nameof(DriverSampleRateKHzHr));
            }
        }

        public int AudioSampleRateKHz
        {
            get
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.DAB:
                        return 0; // TODO: show value when playing DAB
                    default:
                        return _appSettings.FMAudioSampleRate;
                }
            }
            set
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.DAB:
                        _appSettings.DABDriverSampleRate = value;
                        break;
                    default:
                        _appSettings.FMDriverSampleRate = value;
                        break;
                }

                OnPropertyChanged(nameof(AudioSampleRateKHz));
                OnPropertyChanged(nameof(AudioSampleRateKHzHr));
            }
        }

        public string FrequencyKHzHr
        {
            get
            {
                return Convert.ToInt32(FrequencyKHz / 1000) + " KHz";
            }
        }

        public string DriverSampleRateKHzHr
        {
            get
            {
                return Convert.ToInt32(DriverSampleRateKHz / 1000) + " KHz";
            }
        }

        public string AudioSampleRateKHzHr
        {
            get
            {
                return Convert.ToInt32(AudioSampleRateKHz / 1000) + " KHz";
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
                //if (_driver == null)
                return "";

                //if (_driver.DemodulationBitrate > 1000000)
                //{
                //    return (_driver.DemodulationBitrate / 1000000).ToString("N0") + " Mb/s";
                //}
                //else
                //{
                //    return (_driver.DemodulationBitrate / 1000).ToString("N0") + " Kb/s";
                //}
            }
        }

        public string DriverIcon
        {
            get
            {
                if (_driver == null || _driver.State != DriverStateEnum.Connected)
                {
                    return "disconnected.png";
                }

                return "connected.png";
            }
        }

        public string RecordIcon
        {
            get
            {
                //if (_driver == null || (!_driver.RecordingRawData && !_driver.RecordingFMData) )
                //{
                    return "record";
                //}

                //return "stoprecord";
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
                //if (_driver == null || _driver.State != DriverStateEnum.Connected)
                //{
                    return false;
                //}

                //return _driver.RecordingRawData;
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
