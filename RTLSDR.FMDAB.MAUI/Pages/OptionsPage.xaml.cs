using LoggerService;
using RTLSDR;
using RTLSDRReceiver.ViewModels;

namespace RTLSDRReceiver;

public partial class OptionsPage : ContentPage
{
	private ILoggingService _loggingService;
    private ISDR _driver;
    private OptionsViewModel _viewModel;
    private IDialogService _dialogService;

    public OptionsPage(ILoggingService loggingService, ISDR driver, IAppSettings appSettings)
	{
		InitializeComponent();

		_loggingService = loggingService;
		_driver = driver;
        _dialogService = new DialogService();

        BindingContext = _viewModel = new OptionsViewModel(_loggingService, _driver, _dialogService, appSettings);
    }
}