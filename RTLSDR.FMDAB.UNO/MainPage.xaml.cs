using LoggerService;
using RTLSDR.Audio;
using RTLSDR.DAB;
using RTLSDR.Common;
using Microsoft.UI.Dispatching;
using Windows.UI.Core;
using System.Runtime.InteropServices;
using RTLSDR.FM;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System.Profile;
using System.Threading;

namespace RTLSDR.FMDAB.UNO;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; set; }

    private ILoggingService _logger = null;
    private IRawAudioPlayer _audioPlayer = null;
    private bool _rawAudioPlayerInitialized = false;
    private ISDR _sdrDriver = null;
    private IDemodulator _demodulator = null;
    private Thread _updateStatThread = null;

    private double _width = 0;
    private double _height = 0;

    private double _freqCanvasWidth = 0;
    private double _freqCanvasHeight = 0;

    private CancellationTokenSource _cancellationTokenSource;

    public MainPage()
    {
        this.InitializeComponent();

        this.DataContext = ViewModel = new MainPageViewModel();



        var driverInitializationResult = new DriverInitializationResult();

        var deviceFamily = AnalyticsInfo.VersionInfo.DeviceFamily;

        if ((deviceFamily == "Windows.Desktop")
            ||
            (deviceFamily == "Win32NT.Desktop"))
        {
            _audioPlayer = new NAudioRawAudioPlayer(null);
            driverInitializationResult.OutputRecordingDirectory = "c:\\temp";

            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _logger = new NLogLoggingService(System.IO.Path.Combine(appPath, "NLog.config"));
        }
        else if (deviceFamily == "Unix.Unknown")
        {
            _audioPlayer = new AlsaSoundAudioPlayer();
            driverInitializationResult.OutputRecordingDirectory = "/temp";
            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _logger = new NLogLoggingService(System.IO.Path.Combine(appPath, "NLog.config"));
        }
        else if (deviceFamily == "Android.Mobile")
        {
            // TODO: init Android Audio
            _audioPlayer = new NoAudioRawAudioPlayer();

            // TODO: init NLOG (using assets?)
            _logger = new BasicLoggingService();

            driverInitializationResult.OutputRecordingDirectory = "/storage/emulated/0/Android/media/net.petrjanousek.RTLSDRReceiver/";
        } else
        {
            _audioPlayer = new NoAudioRawAudioPlayer();
        }

        AppDomain.CurrentDomain.UnhandledException += (s,e) =>
        {
            _logger.Error(e.ExceptionObject as Exception);
        };


        _sdrDriver = new RTLTCPIPDriver(_logger);
        //_sdrDriver = new RTLSRDTestDriver(_logger);
        _sdrDriver.Init(driverInitializationResult);

        _sdrDriver.SetSampleRate(2048000);

        _sdrDriver.OnDataReceived += (sender, onDataReceivedEventArgs) =>
        {
            if (_demodulator != null)
            {
                _demodulator.AddSamples(onDataReceivedEventArgs.Data, onDataReceivedEventArgs.Size);
            }
        };

        ViewModel.Frequency = AppArguments.Frequency;

        TuneFreq(ViewModel.Frequency);

        this.Loaded += (s, e) =>
        {
            this.Height = Window.Current.Bounds.Height;
            this.Width = Window.Current.Bounds.Width;
        };

        this.Unloaded += (s, e) =>
        {
            _sdrDriver.Disconnect();
        };

        _updateStatThread = new Thread(UpdateStat);
        _updateStatThread.Start();

        LayoutUpdated+= delegate
        {
            _logger.Debug($"Layout updated: {Window.Current.Bounds.Width}, {Window.Current.Bounds.Height}");

            if (
                (_width != Window.Current.Bounds.Width && Window.Current.Bounds.Width != 0)
                ||
                (_height != Window.Current.Bounds.Height && Window.Current.Bounds.Height != 0)
               )
            {
                _width = Window.Current.Bounds.Width;
                _height = Window.Current.Bounds.Height;
                DrawFreqPad(ViewModel.Frequency);
            }
        };
    }

    private void TuneFreq(int freq)
    {
        if (_demodulator != null)
        {
            _demodulator.Stop();
        }

        ViewModel.ClearServicies();

        _sdrDriver.SetFrequency(freq);

        var DABProcessor = new DABProcessor(_logger);
        DABProcessor.OnServiceFound += DABProcessor_OnServiceFound;
        DABProcessor.OnServicePlayed += DABProcessor_OnServicePlayed;
        //DABProcessor.ServiceNumber = 3889;

        _demodulator = DABProcessor;

        _demodulator.OnDemodulated += AppConsole_OnDemodulated;
        //_demodulator.OnSpectrumDataUpdated += _demodulator_OnSpectrumDataUpdated;
    }

    private void DrawFreqPad(double actualFreq)
    {
        FreqCanvas.Children.Clear();

        var freqCountOnPad = 10;

        var leftFrequencies = new Dictionary<double, string>();
        var rightFrequencies = new Dictionary<double, string>();;

        // find count of frequencies left and right
        foreach (var freq in DABConstants.DABFrequenciesMHz)
        {
            if (actualFreq/1000000.0>freq.Key)
            {
                leftFrequencies.Add(freq.Key, freq.Value);
            } else
            {
                rightFrequencies.Add(freq.Key, freq.Value);
            }
        }

        var frequenciesLeftOnLeft = freqCountOnPad/2;
        var frequenciesLeftOnRight = freqCountOnPad/2;

        if (leftFrequencies.Count<freqCountOnPad/2)
        {
            frequenciesLeftOnRight = freqCountOnPad-leftFrequencies.Count;
        } else
        if (rightFrequencies.Count<freqCountOnPad/2)
        {
            frequenciesLeftOnLeft = freqCountOnPad-rightFrequencies.Count;
        }

        if (frequenciesLeftOnLeft>0)
        {
            leftFrequencies = leftFrequencies
            .Reverse() // Reverse the dictionary order
            .Take(frequenciesLeftOnLeft) // Take the last N items
            .Reverse() // Reverse again to restore original order
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        if (frequenciesLeftOnRight>0)
        {
            rightFrequencies = rightFrequencies
            .Take(frequenciesLeftOnRight)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        var allFrequencies = leftFrequencies.Concat(rightFrequencies)
        .ToDictionary(pair => pair.Key, pair => pair.Value);

        var freqWidthPerItem = (_freqCanvasWidth) / freqCountOnPad;

        var i = 0;
        double firstFreq = 0, lastFreq = 0;
        foreach (var freq in allFrequencies)
        {
            if (i == 0)
            {
                firstFreq = freq.Key;
            }

            if (i == allFrequencies.Count - 1)
            {
                lastFreq = freq.Key;
            }

            var leftPos = i * freqWidthPerItem;

            var freqItemText = new TextBlock
            {
                Text = freq.Value,
                FontSize = 16,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255,0,0,0))
            };

            FreqCanvas.Children.Add(freqItemText);
            Canvas.SetLeft(freqItemText, leftPos);
            Canvas.SetTop(freqItemText, 0);

            i++;
        }

        var currentFreqLeftPos = (actualFreq/1000000.0-firstFreq)*((_freqCanvasWidth-freqWidthPerItem)/(lastFreq-firstFreq));

        // draw current freq
        FreqCanvas.Children.Add(new Line
        {
            X1 = currentFreqLeftPos,
            Y1 = 0,
            X2 = currentFreqLeftPos,
            Y2 = _freqCanvasHeight,
            StrokeThickness = 4,
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255,255,0,0))
        });
    }

    private void StatStackPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {

    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _freqCanvasHeight = e.NewSize.Height;
        _freqCanvasWidth = e.NewSize.Width;
    }

    private void UpdateStat()
    {
        while (true)
        {
            if (_demodulator is DABProcessor dab)
            {
                ViewModel.State = dab.State;
                ViewModel.UpdateGUI();
            }

            Thread.Sleep(1000);
        }
    }

    private void OnServiceClick(object sender, ItemClickEventArgs e)
    {
        if ((e.ClickedItem is DABService s) &&
            (_demodulator is DABProcessor dab)
           )
        {
            dab.SetProcessingService(s);
        }
    }

    private async void ButtonTuneLeft_Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SetPreviousFrequency();
        DrawFreqPad(ViewModel.Frequency);

        await DoDelayedAction(
           () =>
           {
                TuneFreq(ViewModel.Frequency);
           }, 1000);
    }

    private async void ButtonTuneRight_Clicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNextFrequency();
        DrawFreqPad(ViewModel.Frequency);

        await DoDelayedAction(
        () =>
        {
            _logger.Info($"Tuning {ViewModel.Frequency}");
            TuneFreq(ViewModel.Frequency);
        }, 1000);
    }

    private async Task DoDelayedAction(Action action, int msDelay)
    {
        try
        {
            // Cancel any previous action
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            // Delay for 2 seconds
            await Task.Delay(1000, _cancellationTokenSource.Token);

            action();
        }
        catch (TaskCanceledException)
        {
            // Action was canceled
        }
    }

    private async void DABProcessor_OnServiceFound(object sender, EventArgs e)
    {
        try
        {
            if (e is DABServiceFoundEventArgs s)
            {
                _logger.Info($"DABProcessor_OnServiceFound - {s.Service.ServiceName}");
                ViewModel.AddService(s.Service);

                if ((ViewModel.SelectedService != null) &&
                    (ViewModel.SelectedService == s.Service)
                    )
                {
                    // works only when breakpoint set!
                    ViewModel.UpdateGUI();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
        }
    }

    private void DABProcessor_OnServicePlayed(object sender, EventArgs e)
    {
        if (e is DABServicePlayedEventArgs dab)
        {
            _logger.Info($"DABProcessor_OnServicePlayed - {dab.Service.ServiceName}");
            ViewModel.SetActiveDABService(dab.Service);
        }
    }

    private void AppConsole_OnDemodulated(object sender, EventArgs e)
    {
        if (e is DataDemodulatedEventArgs ed)
        {
            if (ed.Data == null || ed.Data.Length == 0)
            {
                return;
            }

            try
            {
                if (_audioPlayer != null)
                {
                    if (!_rawAudioPlayerInitialized)
                    {
                        _audioPlayer.Init(ed.AudioDescription, _logger);
                        _audioPlayer.Play();
                        _rawAudioPlayerInitialized = true;
                    }

                    _audioPlayer.AddPCM(ed.Data);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }
    }

    private void DrawSpectrum(System.Drawing.Point[] data, int ymax)
    {
       // TODO: draw spectrum
    }

    private void _demodulator_OnSpectrumDataUpdated(object? sender, EventArgs e)
    {
        if (e is SpectrumUpdatedEventArgs sea)
        {
            Task.Run(() =>
            {
                ViewModel.SyncContext.Post(_ => DrawSpectrum(sea.Data, sea.ymax), null);
            });
        }

    }

}

