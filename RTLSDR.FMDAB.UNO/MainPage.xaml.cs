using LoggerService;
using RTLSDR.Audio;
using RTLSDR.DAB;
using RTLSDR.Common;

namespace RTLSDR.FMDAB.UNO;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; set; }

    private ILoggingService _logger = null;
    private IRawAudioPlayer _audioPlayer = null;
    private bool _rawAudioPlayerInitialized = false;
    private ISDR _sdrDriver = null;
    private IDemodulator _demodulator = null;


    public MainPage()
    {
        this.InitializeComponent();        

        this.DataContext = ViewModel = new MainPageViewModel();

        var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        _logger = new NLogLoggingService( Path.Combine(appPath,"NLog.config"));

        AppDomain.CurrentDomain.UnhandledException += (s,e) =>
        {
            _logger.Error(e.ExceptionObject as Exception);
        };

#if OS_LINUX
            _audioPlayer = new AlsaSoundAudioPlayer();
            //var audioPlayer = new VLCSoundAudioPlayer();
#elif OS_WINDOWS64
            _audioPlayer = new NAudioRawAudioPlayer(null);
            //var audioPlayer = new VLCSoundAudioPlayer();
#else
            _audioPlayer = new NoAudioRawAudioPlayer();
#endif

            _sdrDriver = new RTLTCPIPDriver(_logger);
            _sdrDriver.SetFrequency(192352000);
            _sdrDriver.SetSampleRate(2048000);

            var DABProcessor = new DABProcessor(_logger);
            DABProcessor.OnServiceFound += DABProcessor_OnServiceFound;
            DABProcessor.OnServicePlayed += DABProcessor_OnServicePlayed;
            DABProcessor.ServiceNumber = 3889;            
            _demodulator = DABProcessor;

            _demodulator.OnDemodulated += AppConsole_OnDemodulated;

            _sdrDriver.OnDataReceived += (sender, onDataReceivedEventArgs) =>
            {
                 _demodulator.AddSamples(onDataReceivedEventArgs.Data, onDataReceivedEventArgs.Size);
            };

            _sdrDriver.Init(new DriverInitializationResult()
            {
                OutputRecordingDirectory = appPath
            });            
    }

    private void OnTuneButtonClicked(object sender, EventArgs e)
    {        
        //ViewModel.Frequency = 104000000;
    } 

    private void DABProcessor_OnServiceFound(object sender, EventArgs e)
    {

    }

    private void DABProcessor_OnServicePlayed(object sender, EventArgs e)
    {

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

