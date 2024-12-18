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

    public MainPage()
    {
        this.InitializeComponent();

        this.DataContext = ViewModel = new MainPageViewModel();

        var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        _logger = new NLogLoggingService(System.IO.Path.Combine(appPath,"NLog.config"));

        AppDomain.CurrentDomain.UnhandledException += (s,e) =>
        {
            _logger.Error(e.ExceptionObject as Exception);
        };

        var driverInitializationResult = new DriverInitializationResult();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
             _audioPlayer = new AlsaSoundAudioPlayer();
            driverInitializationResult.OutputRecordingDirectory = "/temp";
        } else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
             _audioPlayer = new NAudioRawAudioPlayer(null);
            driverInitializationResult.OutputRecordingDirectory = "c:\\temp";
        } else
        {
            _audioPlayer = new NoAudioRawAudioPlayer();
        }

        //_sdrDriver = new RTLTCPIPDriver(_logger);

        _sdrDriver = new RTLSRDTestDriver(_logger);


        _sdrDriver.SetFrequency(192352000);
        _sdrDriver.SetSampleRate(2048000);

        var DABProcessor = new DABProcessor(_logger);
        DABProcessor.OnServiceFound += DABProcessor_OnServiceFound;
        DABProcessor.OnServicePlayed += DABProcessor_OnServicePlayed;
        //DABProcessor.ServiceNumber = 3889;
        _demodulator = DABProcessor;

        _demodulator.OnDemodulated += AppConsole_OnDemodulated;
        _demodulator.OnSpectrumDataUpdated += _demodulator_OnSpectrumDataUpdated;

        _sdrDriver.OnDataReceived += (sender, onDataReceivedEventArgs) =>
        {
                _demodulator.AddSamples(onDataReceivedEventArgs.Data, onDataReceivedEventArgs.Size);
        };

        _sdrDriver.Init(driverInitializationResult);

        this.Loaded += (s, e) =>
        {
            this.Height = Window.Current.Bounds.Height;
            this.Width = Window.Current.Bounds.Width;
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
                DrawOnCanvas();
            }            
        };        
    }

    private void DrawOnCanvas()
    {
        FreqCanvas.Children.Clear();

        var freqPadWidth = Window.Current.Bounds.Width;
        var freqWidthPerItem = freqPadWidth / DABConstants.DABFrequenciesMHz.Count;

        var i = 0;
        foreach (var freq in DABConstants.DABFrequenciesMHz)
        {
            var leftPos = i * freqWidthPerItem;

            var text = freq.Value;
            if (!text.Contains('A'))
            {
                text = text.Substring(text.Length-1);
            }

            var freqItemText = new TextBlock
            {
                Text = text,
                FontSize = 16,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255,0,0,0))
            };

            FreqCanvas.Children.Add(freqItemText);
            Canvas.SetLeft(freqItemText, leftPos); 
            Canvas.SetTop(freqItemText, 0);

            i++;
        }
        
        //var availableSize = new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity);

        FreqCanvas.Children.Add(new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = Window.Current.Bounds.Width,
            Y2 = Window.Current.Bounds.Height,
            StrokeThickness = 4,
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255,100,100,100))
        });


        /*
        var availableSize = new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity);

        // Measure the TextBlock
        sliderValueText.Measure(availableSize);

        // Get the desired size after measurement
        var textWidth = sliderValueText.DesiredSize.Width;
        */    
    }

    private void _demodulator_OnSpectrumDataUpdated(object? sender, EventArgs e)
    {

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

    private void OnTuneButtonClicked(object sender, RoutedEventArgs e)
    {
        //ViewModel.Frequency = 104000000;
        if (_demodulator is DABProcessor dab)
        {

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

}

