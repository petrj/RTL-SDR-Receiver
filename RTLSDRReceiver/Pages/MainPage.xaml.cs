using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using RTLSDRReceiver;
using LoggerService;
using RTLSDR;
using System.Diagnostics;
using System.ComponentModel;

namespace RTLSDRReceiver
{
    public partial class MainPage : ContentPage
    {
        private ILoggingService _loggingService;
        private RTLSDR.RTLSDR _driver;
        private MainPageViewModel _viewModel;
        private DialogService _dialogService;

        private double _panStartFrequency = -1;
        private int _panGestureId = -1;
        private DateTime _lastPanUpdateTime = DateTime.MinValue;

        private System.Timers.Timer _panCompletedTimer;

        public MainPage(ILoggingProvider loggingProvider)
        {
            InitializeComponent();

            _loggingService = loggingProvider.GetLoggingService();
            _driver = new RTLSDR.RTLSDR(_loggingService);
            _dialogService = new DialogService(this);

            _loggingService.Info("App started");

            BindingContext = _viewModel = new MainPageViewModel(_loggingService, _driver);

            SubscribeMessages();

            _panCompletedTimer = new System.Timers.Timer(500);
            _panCompletedTimer.Elapsed += _panCompletedTimer_Elapsed;
            _panCompletedTimer.Start();
        }

        private void _panCompletedTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_lastPanUpdateTime == DateTime.MinValue)
            {
                return;
            }

            var sp = (DateTime.Now - _lastPanUpdateTime).TotalMilliseconds;

            if (sp > 500)
            {
                _lastPanUpdateTime = DateTime.MinValue;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _viewModel.RoundFreq();
                    FrequencyPicker.FrequencyKHz = _viewModel.FrequencyKHz;
                    _viewModel.ReTune();
                    FrequencyPickerGraphicsView.Invalidate();
                });
            }
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

                    //_viewModel.FillGainValues();

                    _viewModel.ReTune();

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
            _driver.Settings.FMSampleRate = _viewModel.FMSampleRate;
            _driver.Settings.SDRSampleRate = _viewModel.SDRSampleRate;

            WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
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
            _driver.Recording = true;
            WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
        }

        private void BtnStopRecord_Clicked(object sender, EventArgs e)
        {
            _driver.Recording = false;
            WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
        }

        private void PanGestureRecognizer_PanUpdated(object sender, PanUpdatedEventArgs e)
        {
            _lastPanUpdateTime = DateTime.Now;

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
                        var ratio = FrequencyPicker.Range / FrequencyPickerGraphicsView.Width;
                        _viewModel.FrequencyKHz = FrequencyPicker.FrequencyKHz = _panStartFrequency - e.TotalX * ratio;
                        Debug.WriteLine($"Running: X: {e.TotalX}, Y: {e.TotalY}, Move: {(e.TotalX * ratio).ToString("N2")} Khz, Freq: {FrequencyPicker.FrequencyKHz}");
                        FrequencyPickerGraphicsView.Invalidate();
                    }
                    break;
                case GestureStatus.Completed:
                    // never called
                    break;
            }
        }

        private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            var optionsPage = new OptionsPage(_loggingService, _driver)
            {
                Gain = _viewModel.Gain,
                AutoGain = _viewModel.AutoGain,
                SampleRate = _viewModel.SDRSampleRate,
                FMSampleRate = _viewModel.FMSampleRate,
                DeEmphasis = _viewModel.DeEmphasis
            };

            optionsPage.Disappearing += delegate
            {
                _viewModel.Gain = optionsPage.Gain;
                _viewModel.AutoGain = optionsPage.AutoGain;
                _viewModel.SDRSampleRate = optionsPage.SampleRate;
                _viewModel.FMSampleRate = optionsPage.FMSampleRate;
                _viewModel.DeEmphasis = optionsPage.DeEmphasis;

                _viewModel.ReTune();
            };
            await Navigation.PushModalAsync(optionsPage);
        }
    }
}