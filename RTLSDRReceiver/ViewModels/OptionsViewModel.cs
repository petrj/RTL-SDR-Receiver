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
    public class OptionsViewModel : BasicViewModel
    {
        public ObservableCollection<GainValue> GainValues { get; set; } = new ObservableCollection<GainValue>();
        public ObservableCollection<SampleRateValue> SampleRates { get; set; } = new ObservableCollection<SampleRateValue>();
        public ObservableCollection<SampleRateValue> FMSampleRates { get; set; } = new ObservableCollection<SampleRateValue>();

        public OptionsViewModel(ILoggingService loggingService, RTLSDR.RTLSDR driver, IDialogService dialogService)
                   : base(loggingService, driver, dialogService)
        {
            _loggingService.Debug("OptionsViewModel");

            FillGainValues();
            FillSampleRates();
        }

        public void FillSampleRates()
        {
            SampleRates.Clear();

            SampleRates.Add(new SampleRateValue(1000000));
            SampleRates.Add(new SampleRateValue(1024000));
            SampleRates.Add(new SampleRateValue(1056000)); // rtl_sd exact sample rate
            SampleRates.Add(new SampleRateValue(1400000)); //
            SampleRates.Add(new SampleRateValue(1600000)); //
            SampleRates.Add(new SampleRateValue(1800000));
            SampleRates.Add(new SampleRateValue(1920000));
            SampleRates.Add(new SampleRateValue(2000000));
            SampleRates.Add(new SampleRateValue(2048000));
            SampleRates.Add(new SampleRateValue(2400000));

            FMSampleRates.Clear();

            FMSampleRates.Add(new SampleRateValue(11000));
            FMSampleRates.Add(new SampleRateValue(22000));
            FMSampleRates.Add(new SampleRateValue(32000));
            FMSampleRates.Add(new SampleRateValue(88200));
            FMSampleRates.Add(new SampleRateValue(48000));
            FMSampleRates.Add(new SampleRateValue(96000));
            FMSampleRates.Add(new SampleRateValue(176400));
            FMSampleRates.Add(new SampleRateValue(192000));
            FMSampleRates.Add(new SampleRateValue(320000));
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

            OnPropertyChanged(nameof(GainValue));
            OnPropertyChanged(nameof(AutoGain));
        }

        public SampleRateValue SampleRateValue
        {
            get
            {
                return GetSampleRateValue(SampleRates, _SDRSampleRate);
            }
            set
            {
                SDRSampleRate = value.Value;

                OnPropertyChanged(nameof(SampleRateValue));
            }
        }

        public SampleRateValue FMSampleRateValue
        {
            get
            {
                return GetSampleRateValue(FMSampleRates, _FMSampleRate);
            }
            set
            {
                FMSampleRate = value.Value;

                OnPropertyChanged(nameof(FMSampleRateValue));
            }
        }

        public GainValue GainValue
        {
            get
            {
                if (AutoGain)
                {
                    return GetGainValue(null);
                }

                return GetGainValue(_gain);
            }
            set
            {
                if (value != null && value.Value != null && value.Value.HasValue)
                {
                    _gain = value.Value.Value;
                    AutoGain = false;
                }
                else
                {
                    AutoGain = true;
                }
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

        private SampleRateValue GetSampleRateValue(ObservableCollection<SampleRateValue> collection, int value)
        {
            foreach (var s in collection)
            {
                if (s.Value == value)
                {
                    return s;
                }
            }

            return null;
        }

    }
}
