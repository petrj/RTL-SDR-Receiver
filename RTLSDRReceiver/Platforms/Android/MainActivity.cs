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
        private int _streamPort;
        private BackgroundWorker _audioWorker;

        private ILoggingService _loggingService;

        private List<byte> Buffer = new List<byte>();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _loggingService = new LoggerProvider().GetLoggingService();

            SubscribeMessages();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            _audioWorker = new BackgroundWorker();
            _audioWorker.WorkerSupportsCancellation = true;
            _audioWorker.DoWork += _audioWorker_DoWork;

            base.OnCreate(savedInstanceState);
        }

        private void _audioWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info("Starting _audioWorker");

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _streamPort);
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            client.Bind(remoteEP);

            var bufferSize = AudioTrack.GetMinBufferSize(96000, ChannelOut.Mono, Encoding.Pcm16bit);
            var audioTrack = new AudioTrack(Android.Media.Stream.Music, 96000, ChannelOut.Mono, Encoding.Pcm16bit, bufferSize, AudioTrackMode.Stream);

            // Start playing audio
            audioTrack.Play();

            byte[] myReadBuffer = new byte[UDPStreamer.MaxPacketSize];

            var buffer = new byte[UDPStreamer.MaxPacketSize];

            while (!_audioWorker.CancellationPending)
            {
                if (client.Available > 0)
                {
                    var bytesRead = client.Receive(buffer);

                    audioTrack.Write(myReadBuffer, 0, bytesRead);
                    audioTrack.Flush();

                    _loggingService.Info($"Bytes read: {bytesRead}");
                }
                else
                {
                    Thread.Sleep(100);
                }
            }

            audioTrack.Stop();

            _loggingService.Info("_audioWorker finished");
        }

        public void PlaySound(int samplingRate, byte[] pcmData)
        {

            AudioTrack audioTrack = new AudioTrack(Android.Media.Stream.Music,
                                                   samplingRate,
                                                   ChannelOut.Mono,
                                                   Android.Media.Encoding.Pcm16bit,
                                                   pcmData.Length*2 ,
                                                   AudioTrackMode.Static);

            audioTrack.Write(pcmData, 0, pcmData.Length);
            audioTrack.Play();

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
                    InitDriver(settings.Port, settings.SampleRate);
                    _streamPort = settings.Streamport;
                }
            });

            WeakReferenceMessenger.Default.Register<TestMessage>(this, (sender, obj) =>
            {
                //var fileName = System.IO.Path.Combine(AndroidAppDirectory, "fm.raw");
                //PlaySound(48000, System.IO.File.ReadAllBytes(fileName));

                    PlaySound(48000, Buffer.ToArray());
                    Buffer.Clear();

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
                    _audioWorker.RunWorkerAsync();
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