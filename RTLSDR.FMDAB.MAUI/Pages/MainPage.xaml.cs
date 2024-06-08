using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using RTLSDRReceiver;
using LoggerService;
using RTLSDR;
//using FM;
using System.Diagnostics;
using static RTLSDR.RTLSDR;
using static System.Runtime.InteropServices.JavaScript.JSType;
using RTLSDR.FM;

namespace RTLSDRReceiver
{
    public partial class MainPage : ContentPage
    {
        private ILoggingService _loggingService;
        private RTLSDR.RTLSDR _driver;
        private MainPageViewModel _viewModel;
        private DialogService _dialogService;
        private IAppSettings _appSettings;

        private double _panStartFrequency = -1;
        private int _panGestureId = -1;
        private static readonly int[] Ranges = new int[] { 1000, 2000, 4000, 6000 };

        public MainPage(ILoggingProvider loggingProvider, IAppSettings appSettings)
        {
            InitializeComponent();

            _loggingService = loggingProvider.GetLoggingService();
            _driver = new RTLSDR.RTLSDR(_loggingService);
            _driver.Demodulator = new FMDemodulator(_loggingService);
            _dialogService = new DialogService(this);
            _appSettings = appSettings;

            _loggingService.Info("App started");

            BindingContext = _viewModel = new MainPageViewModel(_loggingService, _driver, _dialogService, _appSettings);

            _viewModel.FrequencyKHz = _appSettings.FrequencyKHz;
            FrequencyPickerGraphicsView.Invalidate();

            SubscribeMessages();
        }

        private void SubscribeMessages()
        {
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

                    _viewModel.ReTune(true);

                    WeakReferenceMessenger.Default.Send(new ToastMessage($"Driver successfully initialized"));
                    WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                }
            });

            WeakReferenceMessenger.Default.Register<DriverInitializationFailedMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DriverInitializationFailedResult failedResult)
                {
                    _driver.Installed = true;
                    WeakReferenceMessenger.Default.Send(new ToastMessage($"Driver initialization failed ({failedResult.DetailedDescription})"));
                    WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                }
            });

            WeakReferenceMessenger.Default.Register<DriverNotInstalledMessage>(this, (sender, obj) =>
            {
                _driver.Installed = false;
            });

            WeakReferenceMessenger.Default.Register<NotifyUSBStateChangedMessage>(this, (r, m) =>
            {
                CheckDriverState();
            });

            WeakReferenceMessenger.Default.Register<NotifyFrequencyChangedMessage>(this, (sender, obj) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FrequencyPicker.FrequencyKHz = _viewModel.FrequencyKHz;
                    FrequencyPickerGraphicsView.Invalidate();
                });
            });
        }

        private void CheckDriverState()
        {
            _loggingService.Info("Checking driver state");

            if (_driver.State == DriverStateEnum.Connected)
            {
                // waitng 2 secs for checking driver state
                Task.Run(async () =>
                {
                    await Task.Delay(2000);

                    if (_driver.State != DriverStateEnum.Connected)
                    {
                        WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                        WeakReferenceMessenger.Default.Send(new DisconnectDriverMessage());
                    }

                });
            } else
            {
                // try to connect
                InitDriver();
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            InitDriver();

            switch(_appSettings.Mode)
            {
                case ModeEnum.FM: Title = "FM";
                    break;
                case ModeEnum.DAB:
                    Title = "DAB+";
                    break;
            }
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
                    WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                    WeakReferenceMessenger.Default.Send(new DisconnectDriverMessage());
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
            if (_driver.State != DriverStateEnum.Connected)
            {
                WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
            }
        }

        private async void BtnDisconnect_Clicked(object sender, EventArgs e)
        {
            if (_driver.State == DriverStateEnum.Connected)
            {
                _driver.Disconnect();
                WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                WeakReferenceMessenger.Default.Send(new DisconnectDriverMessage());
            }
            else
            {
                await _dialogService.Information($"Device not connected");
            }
        }

        private async void BtnConnect_Clicked(object sender, EventArgs e)
        {
            if (_driver.State == DriverStateEnum.Connected)
            {
                await _dialogService.Information($"Device already connected");
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
                    InitDriver();
                }
            }
        }

        private void BtnRecord_Clicked(object sender, EventArgs e)
        {

        }

        private void BtnStopRecord_Clicked(object sender, EventArgs e)
        {

        }

        private void PanGestureRecognizer_PanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    Debug.WriteLine($"Starting: X: {e.TotalX}, Y: {e.TotalY}");

                    _panStartFrequency = _viewModel.FrequencyKHz;
                    _panGestureId = e.GestureId;

                    break;
                case GestureStatus.Running:
                    if (e.GestureId == _panGestureId)
                    {
                        Debug.WriteLine($"Running: X: {e.TotalX}, Y: {e.TotalY}");

                        var ratio = FrequencyPicker.Range / FrequencyPickerGraphicsView.Width;
                        _viewModel.FrequencyKHz = Convert.ToInt32(_panStartFrequency - e.TotalX * ratio);
                        FrequencyPicker.FrequencyKHz = _viewModel.FrequencyKHz;
                        FrequencyPickerGraphicsView.Invalidate();
                    }
                    break;
                case GestureStatus.Completed:
                    if (e.GestureId == _panGestureId)
                    {
                        Debug.WriteLine($"Completed: X: {e.TotalX}, Y: {e.TotalY}");
                        _panGestureId = -1;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            _viewModel.RoundFreq();
                            _viewModel.ReTune(false);
                            FrequencyPicker.FrequencyKHz = _viewModel.FrequencyKHz;
                            FrequencyPickerGraphicsView.Invalidate();
                        });
                    }
                    break;
                case GestureStatus.Canceled:
                    Debug.WriteLine($"Canceled: X: {e.TotalX}, Y: {e.TotalY}");
                    break;
            }
        }

        private async void ToolRecord_Clicked(object sender, EventArgs e)
        {
            if (_driver.RecordingRawData || _driver.RecordingFMData)
            {
                _driver.RecordingRawData = false;
                _driver.RecordingFMData = false;
            }
            else
            {
                var recordChoice = await _dialogService.Select(new List<string>() { "Raw IQ data", "FM PCM" }, "Record");

                if (recordChoice == "Raw IQ data")
                {
                    _driver.RecordingRawData = true;
                }
                else
                    if (recordChoice == "FM PCM")
                {
                    _driver.RecordingFMData = true;
                }
            }

            WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
        }

        private async void ToolOptions_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new OptionsPage(_loggingService, _driver, _appSettings));
        }

        private void PinchGestureRecognizer_PinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            switch (e.Status)
            {
                case GestureStatus.Started:
                    Debug.WriteLine($"Started: Scale: {e.Scale}");
                    break;
                case GestureStatus.Running:
                    Debug.WriteLine($"Running: Scale: {e.Scale}");

                    int pos = 0;
                    if (e.Scale>1)
                    {
                        // zoom in
                        pos = Ranges.Length-1;
                        while (pos > 0 && Ranges[pos] >= FrequencyPicker.Range)
                        {
                            pos --;
                        }
                    } else if (e.Scale < 1)
                    {
                        // zoom out
                        pos = 0;
                        while (pos < Ranges.Length - 1 && Ranges[pos] <= FrequencyPicker.Range)
                        {
                            pos++;
                        }
                    }
                    FrequencyPicker.Range = Ranges[pos];
                    FrequencyPickerGraphicsView.Invalidate();
                    break;
                case GestureStatus.Completed:
                    Debug.WriteLine($"Completed: Scale: {e.Scale}");
                    break;
                case GestureStatus.Canceled:
                    Debug.WriteLine($"Canceled: Scale: {e.Scale}");
                    break;
            }
        }

        private void ButtonTuneLeft_Clicked(object sender, EventArgs e)
        {
            _viewModel.AutoTune(-100);
        }

        private void ButtonTuneRight_Clicked(object sender, EventArgs e)
        {
            _viewModel.AutoTune(100);
        }

        private async void ButtonStat_Clicked(object sender, EventArgs e)
        {
            var testData = "/storage/emulated/0/Android/media/net.petrjanousek.RTLSDRReceiver/test.raw";
            if (!File.Exists(testData))
            {
                await _dialogService.Information("File test.raw not found");
                return;
            }

            var resAtan = await _dialogService.Select(new List<string>() { "Std atan", "Fast atan" });
            var fastAtan = resAtan == "Fast atan";

            var resParallel = await _dialogService.Select(new List<string>() { "Single thread", "Parallel", "Single thread optimalized" });

            DemodAlgorithmEnum demod = DemodAlgorithmEnum.SingleThread;
            switch (resParallel)
            {
                case "Single thread":
                    demod = DemodAlgorithmEnum.SingleThread; break;
                case "Parallel":
                    demod = DemodAlgorithmEnum.Parallel; break;
                case "Single thread optimalized":
                    demod = DemodAlgorithmEnum.SingleThreadOpt; break;
            }

            _viewModel.StatVisible = false;

            await Task.Run( async () =>
            {
                var bytes = File.ReadAllBytes(testData);

                WeakReferenceMessenger.Default.Send(new ChangeSampleRateMessage(96000)); // will start audio thread in MainActivity

                /*
                var fm = new FMDemodulator(_loggingService);
                fm.AddSamples(bytes, bytes.Length);
                fm.OnFinished += delegate
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await _dialogService.Information("Done");
                        _viewModel.StatVisible = true;
                    });
                };
                fm.Finish();
                */
            });
        }

        private async void ToolMode_Clicked(object sender, EventArgs e)
        {
            var modeChoice = await _dialogService.Select(new List<string>() { "FM", "DAB+" }, "FM");

            if (modeChoice == "FM")
            {
                _appSettings.Mode = ModeEnum.FM;
            }
            else
            if (modeChoice == "DAB+")
            {
                _appSettings.Mode = ModeEnum.DAB;
            }
        }
    }
}