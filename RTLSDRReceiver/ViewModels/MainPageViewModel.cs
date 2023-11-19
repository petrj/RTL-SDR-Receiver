using CommunityToolkit.Mvvm.Messaging;
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

            var steps = 0;

            if (e.Argument != null && e.Argument is double stepKHz)
            {
                while (!_tuningWorker.CancellationPending)
                {
                    FrequencyKHz += stepKHz;

                    ReTune();

                    Thread.Sleep(5000);

                    var pwr = _driver.Power;
                    var pwrp = _driver.PowerPercent;

                    _loggingService.Debug($" ----===( FrequencyKHzL {FrequencyKHz}, Power: {pwr}, Power %: {pwrp}");

                    steps++;
                    if (steps > 10)
                    {
                        break;
                    }
                }
            }

            _loggingService.Debug("Auto tuning finished");
        }

        private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            OnPropertyChanged(nameof(RTLBitrate));
            OnPropertyChanged(nameof(DemodulationBitrate));
            OnPropertyChanged(nameof(PowerPercent));
            OnPropertyChanged(nameof(Power));
            OnPropertyChanged(nameof(PowerLabel));
            OnPropertyChanged(nameof(PowerPercentProgress));
            OnPropertyChanged(nameof(PowerPercentLabel));
        }

        public void ReTune()
        {
            _loggingService.Info("Retune");

            if (_driver.State == DriverStateEnum.Connected)
            {
                _driver.ClearAudioBuffer();

                _driver.SetDirectSampling(0);
                _driver.SetFrequencyCorrection(0);
                _driver.SetGainMode(!AutoGain);

                _driver.SetFrequency(Frequency);
                _driver.SetSampleRate(SDRSampleRate);

                //_driver.SetAGCMode(!AutoGain);
                //_driver.SetIfGain(!AutoGain);

                WeakReferenceMessenger.Default.Send(new ChangeSampleRateMessage(_FMSampleRate));
            }
        }

        public async void AutoTune(double stepKHz = 100)
        {
            if (_tuningWorker.IsBusy)
            {
                // message
                await _dialogService.Information("Tuning in progress");
                return;
            }

            _tuningWorker.RunWorkerAsync(stepKHz);
        }
    }
}
