using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using RTLSDR;
using RTLSDR.DAB;
using RTLSDR.FM;
using RTLSDRReceiver.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class MainPageViewModel : BasicViewModel
    {
        private BackgroundWorker _tuningWorker;
        private bool _tuningInProgress = false;
        private double _minPowerSignalTreshold = 55.00;
        private bool _statVisible = true;

        public MainPageViewModel(ILoggingService loggingService, ISDR driver, IDialogService dialogService, IAppSettings appSettings)
            : base(loggingService, driver, dialogService, appSettings)
        {
            _loggingService.Debug("MainPageViewModel");

            var updateTimer = new System.Timers.Timer(2000);
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();

            _tuningWorker = new BackgroundWorker();
            _tuningWorker.WorkerReportsProgress = true;
            _tuningWorker.WorkerSupportsCancellation = true;
            _tuningWorker.DoWork += _tuningWorker_DoWork;
        }

        private void _tuningWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Debug("Auto tuning started");

            _tuningInProgress = true;

            OnPropertyChanged(nameof(TuneLeftIcon));
            OnPropertyChanged(nameof(TuneRightIcon));

            if (e.Argument != null && e.Argument is double stepKHz)
            {
                while (!_tuningWorker.CancellationPending)
                {
                    FrequencyKHz += Convert.ToInt32(stepKHz);
                    ReTune(false);

                    WeakReferenceMessenger.Default.Send(new NotifyFrequencyChangedMessage());

                    Thread.Sleep(3000);

                    var pwr = _driver.PowerPercent;

                    _loggingService.Debug($"Auto tuning frequencyKHz: {FrequencyKHz}, Power: {pwr} %");

                    if (pwr > _minPowerSignalTreshold)
                    {
                        _loggingService.Debug($"Signal found: {(FrequencyKHz/1000).ToString("N1")} MHz");
                        break;
                    }

                    if (
                        (stepKHz > 0 && FrequencyKHz > RTLSDR.FM.FMConstants.FMax)
                        ||
                        (stepKHz < 0 && FrequencyKHz < RTLSDR.FM.FMConstants.FMMin)
                       )
                    {
                        break;
                    }
                }
            }

            _tuningInProgress = false;

            OnPropertyChanged(nameof(TuneLeftIcon));
            OnPropertyChanged(nameof(TuneRightIcon));

            _loggingService.Debug("Auto tuning finished");
        }

        private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            OnPropertyChanged(nameof(RTLBitrate));
            OnPropertyChanged(nameof(DemodulationBitrate));
            OnPropertyChanged(nameof(PowerPercent));
            OnPropertyChanged(nameof(PowerPercentProgress));
            OnPropertyChanged(nameof(PowerPercentLabel));

            OnPropertyChanged(nameof(DriverIcon));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsNotConnected));
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
                OnPropertyChanged(nameof(FrequencyWholePartMHz));
                OnPropertyChanged(nameof(FrequencyDecimalPartMHz));
            }
        }

        public string FrequencyWholePartMHz
        {
            get
            {
                return Convert.ToInt64(Math.Floor(FrequencyKHz / 1000.0)).ToString();
            }
        }

        public string FrequencyDecimalPartMHz
        {
            get
            {
                var part = (FrequencyKHz / 1000.0) - Convert.ToInt64(Math.Floor(FrequencyKHz / 1000.0));
                var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                return $".{part1000} MHz";
            }
        }

        /// <summary>
        /// rounding freq
        /// </summary>
        public void RoundFreq()
        {
            switch (_appSettings.Mode)
            {
                case ModeEnum.FM:

                    if ((FrequencyKHz-1000>FMConstants.FMMin) &&
                        (FrequencyKHz+1000<FMConstants.FMax))
                    {
                        //rounding to tenth

                        var freq10Mhz = FrequencyKHz / 100.0;
                        var roundedFreq10Mhz = Math.Round(freq10Mhz);

                        FrequencyKHz = Convert.ToInt32(roundedFreq10Mhz * 100.0);
                    }

                    break;

                case ModeEnum.DAB:

                    if ((FrequencyKHz - 1000 > DABConstants.DABMinKHz) &&
                        (FrequencyKHz + 1000 < DABConstants.DABMaxKHz))
                    {
                        double min = int.MaxValue;
                        int minFreq = 0;
                        foreach (var kvp in DABConstants.DABFrequenciesMHz)
                        {
                            var distance = Math.Abs(kvp.Key - FrequencyKHz / 1000.00);
                            if (distance < min)
                            {
                                min = distance;
                                minFreq = Convert.ToInt32(kvp.Key*1000);
                            }
                        }

                        FrequencyKHz = minFreq;
                    }

                    break;
            }
        }

        public void ReTune(bool force)
        {
            _loggingService.Info("Retune");

            if (_driver.State == DriverStateEnum.Connected)
            {
                if (force)
                {
                    _driver.SetSampleRate(DriverSampleRateKHz);
                    _driver.SetDirectSampling(0);
                    _driver.SetFrequencyCorrection(0);
                    _driver.SetGainMode(false);
                    //_driver.SetAGCMode(!AutoGain);
                    //_driver.SetIfGain(!AutoGain);
                }

                _driver.SetFrequency(FrequencyKHz * 1000);

                //_driver.DeEmphasis = DeEmphasis;
                //_driver.FastAtan = FastAtan;

                //WeakReferenceMessenger.Default.Send(new ChangeSampleRateMessage(_driver.Settings.FMSampleRate));
            }
        }

        public async void AutoTune(double stepKHz = 100)
        {
            if (_tuningInProgress)
            {
                _tuningWorker.CancelAsync();
            }
            else
            {
                _tuningWorker.RunWorkerAsync(stepKHz);
            }
        }

        public string TuneLeftIcon
        {
            get
            {
                if (_driver == null || _driver.State != DriverStateEnum.Connected || !_tuningInProgress)
                {
                    return "\u23EA";
                }

                return "x";
            }
        }

        public string TuneRightIcon
        {
            get
            {
                if (_driver == null || _driver.State != DriverStateEnum.Connected || !_tuningInProgress)
                {
                    return "\u23E9";
                }

                return "x";
            }
        }

        public bool StatVisible
        {
            get
            {
                return _statVisible;
            }
            set
            {
                _statVisible = value;

                OnPropertyChanged(nameof(StatVisible));
                OnPropertyChanged(nameof(StatNotVisible));
            }
        }

        public bool StatNotVisible
        {
            get
            {
                return !_statVisible;
            }
        }
    }
}
