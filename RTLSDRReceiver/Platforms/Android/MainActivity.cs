using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using Google.Android.Material.Snackbar;
using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using RTLSDR;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
using Android.Media;
using Java.Util.Concurrent.Locks;

namespace RTLSDRReceiver
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int StartRequestCode = 1000;
        private const int AudioBufferSize = 1024*1024;

        private object _audioLock = new object();

        private int _streamPort;
        private int _FMSampleRate;

        private BackgroundWorker _audioReceiver;
        private BackgroundWorker _audioWorker;

        private List<byte> _audioBuffer = new List<byte>();

        private ILoggingService _loggingService;

        private AudioTrack _audioTrack;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _loggingService = new LoggerProvider().GetLoggingService();

            SubscribeMessages();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            _audioWorker = new BackgroundWorker();
            _audioWorker.WorkerSupportsCancellation = true;
            _audioWorker.DoWork += _audioWorker_DoWork;

            _audioReceiver = new BackgroundWorker();
            _audioReceiver.WorkerSupportsCancellation = true;
            _audioReceiver.DoWork += _audioReceiver_DoWork;

            base.OnCreate(savedInstanceState);
        }

        private void _audioReceiver_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info("Starting _audioReceiver");

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _streamPort);
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            client.Bind(remoteEP);

            var packetBuffer = new byte[UDPStreamer.MaxPacketSize];

            var lastSecNotification = string.Empty;

            while (!_audioReceiver.CancellationPending)
            {
                if (client.Available > 0)
                {
                    var bytesRead = client.Receive(packetBuffer);
                    var bufferSize = 0;

                    lock (_audioLock)
                    {
                        for (var i = 0; i < bytesRead; i++)
                        {
                            _audioBuffer.Add(packetBuffer[i]);
                        }

                        bufferSize = _audioBuffer.Count;
                    }

                    var thisTimeNotification = DateTime.Now.ToString("yyyyMMddhhmmss");
                    if (thisTimeNotification != lastSecNotification)
                    {
                        _loggingService.Info($" --ooooo-- receiving buffer size: {bufferSize}");

                        lastSecNotification = thisTimeNotification;
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }

            _loggingService.Info("_audioReceiver finished");
        }


        private void _audioWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info("Starting _audioWorker");

            var bufferSize = AudioTrack.GetMinBufferSize(_FMSampleRate, ChannelOut.Mono, Encoding.Pcm16bit);
            _audioTrack = new AudioTrack(Android.Media.Stream.Music, _FMSampleRate, ChannelOut.Mono, Encoding.Pcm16bit, bufferSize, AudioTrackMode.Stream);

            _audioTrack.Play();

            var dataProcessed = false;
            while (!_audioWorker.CancellationPending)
            {
                lock (_audioLock)
                {
                    if (_audioBuffer.Count > AudioBufferSize)
                    {
                        _loggingService.Info($" --ooooo-- writing audio data");

                        _audioTrack.Write(_audioBuffer.ToArray(), 0, _audioBuffer.Count);

                        //var fName = System.IO.Path.Combine(AndroidAppDirectory, $"Audio-data-{DateTime.Now.ToString("yyyyMMddhhmmss")}.fm.raw");
                        //System.IO.File.WriteAllBytes(fName, _audioBuffer.ToArray());

                        _audioBuffer.Clear();

                        dataProcessed = true;
                    } else
                    {
                        dataProcessed = false;
                    }
                }

                if (!dataProcessed)
                {
                    Thread.Sleep(100);
                }
            }

            _audioTrack.Stop();

            _loggingService.Info("_audioWorker finished");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _loggingService.Error(e.ExceptionObject as Exception);
        }

        private void SubscribeMessages()
        {
            WeakReferenceMessenger.Default.Register<InitDriverMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DriverSettings settings)
                {
                    InitDriver(settings.Port, settings.SDRSampleRate);
                    _streamPort = settings.Streamport;
                    _FMSampleRate = settings.FMSampleRate;
                }
            });
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == StartRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    WeakReferenceMessenger.Default.Send(new DriverInitializedMessage(new DriverInitializationResult()
                    {
                        SupportedTcpCommands = data.GetIntArrayExtra("supportedTcpCommands"),
                        DeviceName = data.GetStringExtra("deviceName"),
                        OutputRecordingDirectory = AndroidAppDirectory
                    }));

                    _audioWorker.CancelAsync();
                    _audioReceiver.CancelAsync();

                    _audioWorker.RunWorkerAsync();
                    _audioReceiver.RunWorkerAsync();
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new DriverInitializationFailedMessage(new DriverInitializationFailedResult()
                    {
                        ErrorId = data.GetIntExtra("marto.rtl_tcp_andro.RtlTcpExceptionId", -1),
                        ExceptionCode = data.GetIntExtra("detailed_exception_code", 0),
                        DetailedDescription = data.GetStringExtra("detailed_exception_message")
                    }));
                }
            }
        }

        private void InitDriver(int port = 1234, int samplerate = 2048000)
        {
            try
            {
                var req = new Intent(Intent.ActionView);
                req.SetData(Android.Net.Uri.Parse($"iqsrc://-a 127.0.0.1 -p \"{port}\" -s \"{samplerate}\""));
                req.PutExtra(Intent.ExtraReturnResult, true);

                StartActivityForResult(req, StartRequestCode);
            }
            catch (ActivityNotFoundException ex)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage("Driver not installed"));
                WeakReferenceMessenger.Default.Send(new DriverNotInstalledMessage());
            }
            catch (Exception ex)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage("Driver initializing failed"));
            }
        }

        public static string AndroidAppDirectory
        {
            get
            {
                try
                {
                    // internal storage - always writable directory
                    try
                    {
                        var pathToExternalMediaDirs = Android.App.Application.Context.GetExternalMediaDirs();

                        if (pathToExternalMediaDirs.Length == 0)
                            throw new DirectoryNotFoundException("No external media directory found");

                        return pathToExternalMediaDirs[0].AbsolutePath;
                    }
                    catch
                    {
                        // fallback for older API:

                        var internalStorageDir = Android.App.Application.Context.GetExternalFilesDir(System.Environment.SpecialFolder.MyDocuments.ToString());

                        return internalStorageDir.AbsolutePath;
                    }

                }
                catch
                {
                    var dir = Android.App.Application.Context.GetExternalFilesDir("");

                    return dir.AbsolutePath;
                }
            }
        }
    }
}