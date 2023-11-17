using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using Microsoft.Maui.Dispatching;
using RTLSDR;
using RTLSDRReceiver.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class MainPageViewModel : BasicViewModel
    {
        public MainPageViewModel(ILoggingService loggingService, RTLSDR.RTLSDR driver)
            : base(loggingService, driver)
        {
            _loggingService.Debug("MainPageViewModel");

            var updateTimer = new System.Timers.Timer(2000);
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            OnPropertyChanged(nameof(RTLBitrate));
            OnPropertyChanged(nameof(DemodulationBitrate));
            OnPropertyChanged(nameof(PowerPercent));
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
    }
}
