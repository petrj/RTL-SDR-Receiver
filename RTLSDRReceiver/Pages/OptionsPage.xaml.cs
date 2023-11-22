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
}