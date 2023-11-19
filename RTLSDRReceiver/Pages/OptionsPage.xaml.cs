using LoggerService;
using RTLSDRReceiver.ViewModels;

namespace RTLSDRReceiver;

public partial class OptionsPage : ContentPage
{
	private ILoggingService _loggingService;
    private RTLSDR.RTLSDR _driver;
    private OptionsViewModel _viewModel;
    private IDialogService _dialogService;

    public OptionsPage(ILoggingService loggingService, RTLSDR.RTLSDR driver)
	{
		InitializeComponent();

		_loggingService = loggingService;
		_driver = driver;
        _dialogService = new DialogService();

        BindingContext = _viewModel = new OptionsViewModel(_loggingService, _driver, _dialogService);
    }

    public int Gain
    {
        get
        {
            var gainValue = _viewModel.GainValue;

            if (gainValue != null && gainValue.Value != null && _viewModel.GainValue.Value.HasValue)
            {
                return _viewModel.GainValue.Value.Value;
            }

            return 0;
        }
        set
        {
            _viewModel.GainValue = new GainValue(value);
        }
    }

    public int SampleRate
    {
        get
        {
            return _viewModel.SampleRateValue.Value;
        }
        set
        {
            _viewModel.SampleRateValue = new SampleRateValue(value);
        }
    }

    public int FMSampleRate
    {
        get
        {
            return _viewModel.FMSampleRateValue.Value;
        }
        set
        {
            _viewModel.FMSampleRateValue = new SampleRateValue(value);
        }
    }

    public bool DeEmphasis
    {
        get
        {
            return _viewModel.DeEmphasis;
        }
        set
        {
            _viewModel.DeEmphasis = value;
        }
    }

    public bool AutoGain
    {
        get
        {
            return _viewModel.AutoGain;
        }
        set
        {
            _viewModel.AutoGain = value;
        }
    }
}