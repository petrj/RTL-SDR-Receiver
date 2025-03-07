﻿using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using RTLSDRReceiver;
using LoggerService;
using RTLSDR;
using System.Diagnostics;
using static RTLSDR.RTLSDRDriver;
using static System.Runtime.InteropServices.JavaScript.JSType;
using RTLSDR.FM;
using System.Runtime.CompilerServices;
using RTLSDR.DAB;
using Microsoft.Maui.Graphics.Text;
using static System.Net.Mime.MediaTypeNames;
using Markdig.Extensions.Hardlines;
using RTLSDR.Common;

namespace RTLSDRReceiver
{
    public partial class MainPage : ContentPage
    {
        private ILoggingService _loggingService;
        private ISDR _driver;
        private MainPageViewModel _viewModel;
        private DialogService _dialogService;
        private IAppSettings _appSettings;

        private double _panStartFrequency = -1;
        private int _panGestureId = -1;
        private static readonly int[] Ranges = new int[] { 1000, 2000, 4000, 6000 };
        private bool _firstAppearing = true;
        private bool _installingDriver = false;

        private double _screenWidth = 0;
        private double _screenHeight = 0;

        private List<Label> _freqNumberLabels;
        private Dictionary<double,Label> _DABFreqNumberLabels;

        private UDPStreamer _UDPStreamer = null;

        public MainPage(ILoggingProvider loggingProvider, IAppSettings appSettings)
        {
            InitializeComponent();

            _loggingService = loggingProvider.GetLoggingService();
            _dialogService = new DialogService(this);
            _appSettings = appSettings;

            _loggingService.Info("App started");

            switch (appSettings.DriverType)
            {
                case DriverTypeEnum.RTLSDR_TCP:
                    _driver = new RTLTCPIPDriver(_loggingService);
                    break;
                case DriverTypeEnum.RTLSDR_Android:
                    _driver = new RTLSDR.RTLSDRDriver(_loggingService);
                    break;
                case DriverTypeEnum.Testing_Driver:
                    _driver = new RTLSRDTestDriver(_loggingService);
                    break;
            }

            _driver.OnDataReceived += _driver_OnDataReceived;

            BindingContext = _viewModel = new MainPageViewModel(_loggingService, _driver, _dialogService, _appSettings);

            _UDPStreamer = new UDPStreamer(_loggingService, "127.0.0.1", _driver.Settings.Streamport);

            SubscribeMessages();

        }

        private void _driver_OnDataReceived(object sender, OnDataReceivedEventArgs e)
        {
            if (_viewModel.Demodulator != null)
            {
                _viewModel.Demodulator.AddSamples(e.Data, e.Size);
            }
        }

        private void _demodulator_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs de)
            {
                _UDPStreamer.SendByteArray(de.Data, de.Data.Length);
            }
        }

        private void DrawFreqNumbers()
        {
            if (_screenWidth <=0)
            {
                return;
            }

            if (_freqNumberLabels == null || _freqNumberLabels.Count == 0)
            {
                // 11 labels
                _freqNumberLabels = new List<Label>();

                for (var i = 0; i < 11; i++)
                {
                    // 5 labels left
                    // 1 labels inside
                    // 5 labels right

                    var label = new Label();
                    absoluteLayout.Children.Add(label);
                    _freqNumberLabels.Add(label);
                }
            }

            if (_DABFreqNumberLabels == null || _DABFreqNumberLabels.Count == 0)
            {
                _DABFreqNumberLabels = new Dictionary<double, Label>();

                foreach (var kvp in DABConstants.DABFrequenciesMHz)
                {
                    var label = new Label();
                    absoluteLayout.Children.Add(label);
                    _DABFreqNumberLabels.Add(kvp.Key, label);
                }
            }

            var center = _screenWidth / 2.0;
            var oneKHzScreenWidth = _screenWidth / (FrequencyPicker.Range / 1000.0);

            for (var i = 0; i < 11; i++)
            {
                var label = _freqNumberLabels[i];
                var iShifted = i - 5.0;
                var freq = _viewModel.FrequencyKHz + (iShifted * 1000.0);
                var top = _screenHeight*0.25 + _screenHeight * 0.2 - 30;
                var shift = Math.Round(freq/1000.0) - freq / 1000.0;
                label.Text = (freq/1000.0).ToString("N0");
                label.TextColor = Color.FromRgb(255, 255, 255);
                absoluteLayout.SetLayoutBounds(label, new Rect()
                {
                    Left = center + iShifted * oneKHzScreenWidth + shift*oneKHzScreenWidth,
                    Top = top,
                    Width = 30,
                    Height = 30
                });

                _freqNumberLabels.Add(label);
            }

            foreach (var kvp in DABConstants.DABFrequenciesMHz)
            {
                var centerFreq = _viewModel.FrequencyKHz;

                var label = _DABFreqNumberLabels[kvp.Key];
                var freqKHz = kvp.Key;
                var top = _screenHeight * 0.25 + _screenHeight * 0.2 - 60;
                var offset = freqKHz - _viewModel.FrequencyKHz / 1000.0;
                label.Text = $"{kvp.Value}";
                label.TextColor = Color.FromRgb(255, 255, 255);
                absoluteLayout.SetLayoutBounds(label, new Rect()
                {
                    Left = center + offset*oneKHzScreenWidth,
                    Top = top,
                    Width = 130,
                    Height = 30
                });
            }
        }

        private void SubscribeMessages()
        {
            WeakReferenceMessenger.Default.Register<NotifyResumedMessage>(this, (r, m) =>
            {
                if (_installingDriver)
                {
                    _installingDriver = false;
                    CheckDriverState();
                }
            });

            WeakReferenceMessenger.Default.Register<ToastMessage>(this, (r, m) =>
            {
                Task.Run(async () =>
                {
                    await ShowToastMessage(m.Value);
                });
            });

            WeakReferenceMessenger.Default.Register<NotifyAppSettingsChangeMessage>(this, (sender, settings) =>
            {
                Task.Run(async () =>
                {
                    await UpdateMode();
                });
            });

            WeakReferenceMessenger.Default.Register<DriverInitializedMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DriverInitializationResult settings)
                {
                    _driver.SetFrequency(_viewModel.FrequencyKHz * 1000); // must be set before init due to Test driver
                    _driver.Init(settings);
                    _driver.Installed = true;
                    //_driver.SetSampleRate(_viewModel.DriverSampleRateKHz);

                    _viewModel.ReTune(true);

                    WeakReferenceMessenger.Default.Send(new ToastMessage($"Driver successfully initialized"));
                    WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                }
            });

            WeakReferenceMessenger.Default.Register<DriverInitializationFailedMessage>(this, (sender, obj) =>
            {
                    if (obj.Value is DriverInitializationFailedResult failedResult)
                    {
                        if (_driver.Installed.HasValue && _driver.Installed.Value)
                        {
                            WeakReferenceMessenger.Default.Send(new ToastMessage($"Driver initialization failed ({failedResult.DetailedDescription})"));
                        }
                    _driver.SetErrorState();
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

        private async Task UpdateMode()
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                switch (_appSettings.Mode)
                {
                    case ModeEnum.FM:
                        ButtonFMMode.Style = (Style)Resources["ModeActiveButtonStyle"];
                        ButtonDAMMode.Style = (Style)Resources["ModeInActiveButtonStyle"];
                        break;
                    case ModeEnum.DAB:
                        ButtonFMMode.Style = (Style)Resources["ModeInActiveButtonStyle"];
                        ButtonDAMMode.Style = (Style)Resources["ModeActiveButtonStyle"];
                        break;
                }

                SetFrequency(_viewModel.FrequencyKHz); // updating FreqPicker

                _viewModel.NotifyStateOrConfigurationChange();

                DrawFreqNumbers();

                //CheckDriverState();
            });
        }

        private void SetFrequency(int freqKHz)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _viewModel.FrequencyKHz = freqKHz;
                FrequencyPicker.FrequencyKHz = freqKHz;
                FrequencyPicker.SetFrequencyCaptions(DABConstants.DABFrequenciesMHz);
                FrequencyPickerGraphicsView.Invalidate();
                DrawFreqNumbers();
            });
        }

        private void CheckDriverState()
        {
            _loggingService.Info("Checking driver state");

            _driver.Installed = true;

            if (_driver.State == DriverStateEnum.Connected)
            {
                _loggingService.Info("Driver is connected");
                return;
            }

            // waitng 2 secs for checking driver state
            Task.Run(async () =>
            {
                await Task.Delay(5000);

                if (_driver.State != DriverStateEnum.Connected)
                {
                    _loggingService.Info("Driver NOT connected");

                    WeakReferenceMessenger.Default.Send(new NotifyStateChangeMessage());
                    WeakReferenceMessenger.Default.Send(new DisconnectDriverMessage());
                } else
                {
                    _loggingService.Info("Driver connected");

                    // only checking dirver state -> disconnecting
                    _driver.Disconnect();
                }
            });

            switch (_appSettings.DriverType)
            {
                case DriverTypeEnum.RTLSDR_Android:
                    WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
                    break;
                case DriverTypeEnum.Testing_Driver:
                    WeakReferenceMessenger.Default.Send(new InitTestDriverMessage(_driver.Settings));
                    break;
                case DriverTypeEnum.RTLSDR_TCP:
                    WeakReferenceMessenger.Default.Send(new InitTCPDriverMessage(_driver.Settings));
                    break;
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            _screenWidth = width;
            _screenHeight = height;
            DrawFreqNumbers();
            base.OnSizeAllocated(width, height);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_firstAppearing)
            {
                _firstAppearing = false;
                CheckDriverState();
            }
        }

        protected override void OnDisappearing()
        {
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
                await _dialogService.Information($"Device: {_driver.DeviceName}.");
            }
            else
            {
                if (_driver.Installed.HasValue && !_driver.Installed.Value)
                {
                    if (await _dialogService.Confirm($"Driver not installed.", $"Device status", "Install Driver", "Back"))
                    {
                        _installingDriver = true;
                        await Browser.OpenAsync("https://play.google.com/store/apps/details?id=marto.rtl_tcp_andro", BrowserLaunchMode.External);
                    }
                }
                else
                {
                    if (await _dialogService.Confirm($"Device: {_driver.DeviceName}", $"Device status", "Check driver state", "Back"))
                    {
                        CheckDriverState();
                    }
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
                        SetFrequency(Convert.ToInt32(_panStartFrequency - e.TotalX * ratio));
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
                        });
                    }
                    break;
                case GestureStatus.Canceled:
                    Debug.WriteLine($"Canceled: X: {e.TotalX}, Y: {e.TotalY}");
                    break;
            }
        }

        private async void ButtonRecord_Clicked(object sender, EventArgs e)
        {
           /* if (_driver.RecordingRawData || _driver.RecordingFMData)
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
           */
        }

        private async void ToolOptions_Clicked(object sender, EventArgs e)
        {
            var optionsPage = new OptionsPage(_loggingService, _driver, _appSettings);
            optionsPage.Disappearing += delegate
            {
                // TODO: send message only when something changed
                WeakReferenceMessenger.Default.Send(new NotifyAppSettingsChangeMessage(_appSettings));
                //CheckDriverState();
            };
            await Navigation.PushAsync(optionsPage);
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
            /*
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
            });
            */
        }

        private void ButtonPlay_Clicked(object sender, EventArgs e)
        {

            switch (_appSettings.Mode)
            {
                case ModeEnum.FM:

                    _viewModel.Demodulator = new FMDemodulator(_loggingService);
                    //_driver.Settings.SDRSampleRate = _appSettings.FMDriverSampleRate;

                    WeakReferenceMessenger.Default.Send(new NotifyAudioChangeMessage(new RTLSDR.Common.AudioDataDescription()
                    {
                        BitsPerSample = 16,
                        Channels = 1,
                        SampleRate = _appSettings.FMAudioSampleRate
                    }));

                    break;
                case ModeEnum.DAB:

                    var dabProcessor = new DABProcessor(_loggingService);
                    dabProcessor.ServiceNumber = 3889;
                    _viewModel.Demodulator = dabProcessor;

                    //_driver.Settings.SDRSampleRate = _appSettings.DABDriverSampleRate;

                    WeakReferenceMessenger.Default.Send(new NotifyAudioChangeMessage(new RTLSDR.Common.AudioDataDescription()
                    {
                        BitsPerSample = 16,
                        Channels = 2,
                        SampleRate = 48000
                    }));

                    break;
            }

            _viewModel.Demodulator.OnServiceFound += _demodulator_OnServiceFound;
            _viewModel.Demodulator.OnDemodulated += _demodulator_OnDemodulated;
            _driver.Settings.SDRSampleRate = _viewModel.DriverSampleRateKHz;

            switch (_appSettings.DriverType)
            {
                case DriverTypeEnum.RTLSDR_TCP:
                    WeakReferenceMessenger.Default.Send(new InitTCPDriverMessage(_driver.Settings));
                    break;
                case DriverTypeEnum.RTLSDR_Android:
                    WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
                    break;
                case DriverTypeEnum.Testing_Driver:
                    WeakReferenceMessenger.Default.Send(new InitTestDriverMessage(_driver.Settings));
                    break;
            };
        }

        private void _demodulator_OnServiceFound(object sender, EventArgs e)
        {
            if (e is DABServiceFoundEventArgs edab)
            {
                var service = new DABService() { ServiceName = edab.Service.ServiceName };

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _viewModel.AddService(service);
                });
            };
        }

        private void ButtonStop_Clicked(object sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new NotifyAudioStopMessage());
            if (_viewModel.Demodulator != null)
            {
                _viewModel.Demodulator.Stop();
                _viewModel.Demodulator = null;
            }

            _driver.Disconnect();
        }

        private async void ButtonFMMode_Clicked(object sender, EventArgs e)
        {
            _appSettings.Mode = ModeEnum.FM;
            await UpdateMode();
        }

        private async void ButtonDAMMode_Clicked(object sender, EventArgs e)
        {
            _appSettings.Mode = ModeEnum.DAB;
            await UpdateMode();
        }
    }
}

