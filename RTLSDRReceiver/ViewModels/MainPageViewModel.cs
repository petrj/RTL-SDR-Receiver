﻿using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using Microsoft.Maui.Dispatching;
using RTLSDR;
using RTLSDRReceiver.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class MainPageViewModel : BasicViewModel
    {
        private BackgroundWorker _tuningWorker;
        private bool _tuningInProgress = false;
        private double _minPowerSignalTreshold = 40.00;
        private int _frequencyKHz = 104000;

        public MainPageViewModel(ILoggingService loggingService, RTLSDR.RTLSDR driver, IDialogService dialogService)
            : base(loggingService, driver, dialogService)
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

                    Thread.Sleep(1500);

                    var pwr = _driver.PowerPercent;

                    _loggingService.Debug($"Auto tuning frequencyKHz: {FrequencyKHz}, Power: {pwr} %");

                    if (pwr > _minPowerSignalTreshold)
                    {
                        _loggingService.Debug($"Signal found: {(FrequencyKHz/1000).ToString("N1")} MHz");
                        break;
                    }

                    if (
                        (stepKHz > 0 && FrequencyKHz > RTLSDR.RTLSDR.FMMaxFrequenctKHz)
                        ||
                        (stepKHz < 0 && FrequencyKHz < RTLSDR.RTLSDR.FMMinFrequenctKHz)
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
        }

        public int FrequencyKHz
        {
            get
            {
                return _frequencyKHz;
            }
            set
            {
                _frequencyKHz = value;

                OnPropertyChanged(nameof(FrequencyKHz));
                OnPropertyChanged(nameof(FrequencyWholePartMHz));
                OnPropertyChanged(nameof(FrequencyDecimalPartMHz));
            }
        }

        public string FrequencyWholePartMHz
        {
            get
            {
                return Convert.ToInt64(Math.Floor(_frequencyKHz / 1000.0)).ToString();
            }
        }

        public string FrequencyDecimalPartMHz
        {
            get
            {
                var part = (_frequencyKHz / 1000.0) - Convert.ToInt64(Math.Floor(_frequencyKHz / 1000.0));
                var part1000 = Convert.ToInt64(part * 1000).ToString().PadLeft(3, '0');
                return $".{part1000} MHz";
            }
        }

        /// <summary>
        /// rounding to tenth
        /// </summary>
        public void RoundFreq()
        {
            var freq10Mhz = _frequencyKHz / 100.0;
            var roundedFreq10Mhz = Math.Round(freq10Mhz);

            FrequencyKHz = Convert.ToInt32(roundedFreq10Mhz * 100.0);
        }

        public void ReTune(bool force)
        {
            _loggingService.Info("Retune");

            if (_driver.State == DriverStateEnum.Connected)
            {
                _driver.ClearAudioBuffer();

                if (force)
                {
                    //_driver.SetSampleRate(SDRSampleRate);
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
    }
}