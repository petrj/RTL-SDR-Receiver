using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using RTLSDRReceiver;
using LoggerService;
using RTLSDR;

namespace RTLSDRReceiver
{
    public partial class MainPage : ContentPage
    {
        private ILoggingService _loggingService;
        private RTLSDR.RTLSDR _driver;
        private MainPageViewModel _viewModel;
        private DialogService _dialogService;

        public MainPage(ILoggingProvider loggingProvider)
        {
            InitializeComponent();

            _loggingService = loggingProvider.GetLoggingService();
            _driver = new RTLSDR.RTLSDR(_loggingService);
            _dialogService = new DialogService(this);

            _loggingService.Info("App started");

            BindingContext = _viewModel = new MainPageViewModel(_loggingService, _driver);

            WeakReferenceMessenger.Default.Register<ToastMessage>(this, (r, m) =>
            {
                Task.Run(async () =>
                {
                    await ShowToastMessage(m.Value);
                });
            });

            WeakReferenceMessenger.Default.Register<DriverInitializedMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DriverInitializationResult settings)
                {
                    _driver.Init(settings);
                    _driver.Installed = true;

                    _driver.SetFrequency(_viewModel.Frequency);
                    _driver.SetSampleRate(_viewModel.SDRSampleRate);

                    _driver.SetGainMode(false);

                    if (_driver.TunerType == TunerTypeEnum.RTLSDR_TUNER_E4000)
                    {
                        _driver.SetIfGain(false);
                    }

                    _driver.SetFrequencyCorrection(0);
                    _driver.SetAGCMode(true);

                    _viewModel.FillGainValues();

                    _viewModel.GainValue = null;

                    //_driver.SetGain(0);

                    WeakReferenceMessenger.Default.Send(new ToastMessage($"Driver successfully initialized"));
                    WeakReferenceMessenger.Default.Send(new NotifyDriverIconChangeMessage());
                }
            });

            WeakReferenceMessenger.Default.Register<DriverInitializationFailedMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DriverInitializationFailedResult failedResult)
                {
                    _driver.Installed = true;
                    WeakReferenceMessenger.Default.Send(new ToastMessage($"Driver initialization failed ({failedResult.DetailedDescription})"));
                    WeakReferenceMessenger.Default.Send(new NotifyDriverIconChangeMessage());
                }
            });

            WeakReferenceMessenger.Default.Register<DriverNotInstalledMessage>(this, (sender, obj) =>
            {
                _driver.Installed = false;
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            InitDriver();
        }

        private async Task ShowToastMessage(string message, int seconds = 3, int AppFontSize = 0)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Colors.Gray,
                TextColor = Colors.Black,
                //ActionButtonTextColor = Colors.Blue,
                CornerRadius = new CornerRadius(10),
                Font = Microsoft.Maui.Font.SystemFontOfSize(14, FontWeight.Bold),
                ActionButtonFont = Microsoft.Maui.Font.SystemFontOfSize(14)
                //CharacterSpacing = 0.5
            };

            //Action action = async () => await DisplayAlert("Snackbar ActionButton Tapped", "The user has tapped the Snackbar ActionButton", "OK");

            var snackbar = Snackbar.Make(message, null, "", TimeSpan.FromSeconds(seconds), snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        private async void ToolDriver_Clicked(object sender, EventArgs e)
        {
            if (_driver.State == DriverStateEnum.Connected)
            {
                if (!(await _dialogService.Confirm($"Connected device: {_driver.DeviceName}.", $"Device status", "Back", "Disconnect")))
                {
                    _driver.Disconnect();
                    WeakReferenceMessenger.Default.Send(new NotifyDriverIconChangeMessage());
                }
            }
            else
            {
                if (_driver.Installed.HasValue && !_driver.Installed.Value)
                {
                    if (await _dialogService.Confirm($"Driver not installed.", $"Device status", "Install Driver", "Back"))
                    {
                        await Browser.OpenAsync("https://play.google.com/store/apps/details?id=marto.rtl_tcp_andro", BrowserLaunchMode.External);
                    }
                }
                else
                {
                    if (await _dialogService.Confirm($"Disconnected.", $"Device status", "Connect", "Back"))
                    {
                        InitDriver();
                    }
                }
            }
        }

        private void InitDriver()
        {
            _driver.Settings.FMSampleRate = _viewModel.FMSampleRate;
            _driver.Settings.SDRSampleRate = _viewModel.SDRSampleRate;

            WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
        }

        private void Btn_Clicked(object sender, EventArgs e)
        {
            //_driver.Tune(_viewModel.Frequency, _viewModel.SampleRate);
            //_driver.Recording = !_driver.Recording;
            //WeakReferenceMessenger.Default.Send(new TestMessage());
        }

        private void BtnDisconnect_Clicked(object sender, EventArgs e)
        {

        }

        private void BtnConnect_Clicked(object sender, EventArgs e)
        {

        }
    }
}