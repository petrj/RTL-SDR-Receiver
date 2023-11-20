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
using Android.Hardware.Usb;
using Android.Runtime;

namespace RTLSDRReceiver
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int StartRequestCode = 1000;

        private int _streamPort;
        private int _FMSampleRate;

        private bool _startAudioReceiverThread = false;
        private BackgroundWorker _audioReceiver;
        private bool _startAudioPlayerThread = false;
        private BackgroundWorker _audioPlayer;
        private ILoggingService _loggingService;


        private object _lock = new object();

        private const int MaxAudioBufferSize = 1024 * 1024;
        private Queue<byte[]> _audioBuffer = new Queue<byte[]>();
        private int _audioBufferLength = 0;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _loggingService = new LoggerProvider().GetLoggingService();

            SubscribeMessages();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            _audioReceiver = new BackgroundWorker();
            _audioReceiver.WorkerSupportsCancellation = true;
            _audioReceiver.DoWork += _audioReceiver_DoWork;
            _audioReceiver.RunWorkerCompleted += _audioReceiver_RunWorkerCompleted;

            _audioPlayer = new BackgroundWorker();
            _audioPlayer.WorkerSupportsCancellation = true;
            _audioPlayer.DoWork += _audioPlayer_DoWork;
            _audioPlayer.RunWorkerCompleted += _audioPlayer_RunWorkerCompleted;

            try
            {
                UsbManager manager = (UsbManager)GetSystemService(Context.UsbService);

                var usbReciever = new USBBroadcastReceiverSystem();
                var intentFilter = new IntentFilter(UsbManager.ActionUsbDeviceAttached);
                var intentFilter2 = new IntentFilter(UsbManager.ActionUsbDeviceDetached);
                RegisterReceiver(usbReciever, intentFilter);
                RegisterReceiver(usbReciever, intentFilter2);
                usbReciever.UsbAttachedOrDetached += delegate { WeakReferenceMessenger.Default.Send(new NotifyUSBStateChangedMessage()); };
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, "Error while initializing UsbManager");
            }

            base.OnCreate(savedInstanceState);
        }

        private void _audioPlayer_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_startAudioPlayerThread)
            {
                _audioPlayer.RunWorkerAsync();
                _startAudioPlayerThread = false;
            }
        }

        private void _audioReceiver_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_startAudioReceiverThread)
            {
                _audioReceiver.RunWorkerAsync();
                _startAudioReceiverThread = false;
            }
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
            WeakReferenceMessenger.Default.Register<DisconnectDriverMessage>(this, (sender, obj) =>
            {
                _audioReceiver.CancelAsync();
            });
            WeakReferenceMessenger.Default.Register<ChangeSampleRateMessage>(this, (sender, obj) =>
            {
                if (obj.Value is int v)
                {
                    _FMSampleRate = v;
                    RestartAudio();
                }
            });
        }

        private void _audioPlayer_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info("Starting _audioPlayer");

            var bufferSize = AudioTrack.GetMinBufferSize(_FMSampleRate, ChannelOut.Mono, Encoding.Pcm16bit);
            var _audioTrack = new AudioTrack(Android.Media.Stream.Music, _FMSampleRate, ChannelOut.Mono, Encoding.Pcm16bit, bufferSize, AudioTrackMode.Stream);

            _audioTrack.Play();

            // 1 seconds byte buffer:
            // 320 Kbps =>  40 KBps => 40 000 bytes per sec
            var oneSecAudioBufferSize = 40000;

            byte[] bytesToWrite = new byte[oneSecAudioBufferSize];

            while (!_audioPlayer.CancellationPending)
            {
                var audioBufferBytes = new List<byte>();

                lock (_lock)
                {
                    if (_audioBuffer.Count > 0)
                    {
                        while (_audioBuffer.Count > 0)
                        {
                            audioBufferBytes.AddRange(_audioBuffer.Dequeue());
                        }

                        _audioBufferLength = 0;
                    }
                }

                if (audioBufferBytes.Count > 0)
                {
                    _audioTrack.Write(audioBufferBytes.ToArray(), 0, audioBufferBytes.Count);
                }
                else
                {
                    // no data on input
                    Thread.Sleep(100);
                }
            }

            _audioTrack.Stop();

            _loggingService.Info("_audioPlayer finished");
        }

        private void _audioReceiver_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info("Starting _audioReceiver");

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _streamPort);
            using (Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                client.Bind(remoteEP);

                var packetBuffer = new byte[UDPStreamer.MaxPacketSize];

                while (!_audioReceiver.CancellationPending)
                {
                    if (client.Available > 0)
                    {
                        var bytesRead = client.Receive(packetBuffer);

                        if (bytesRead > 0)
                        {
                            var bytesToSend = new byte[bytesRead];

                            lock (_lock)
                            {
                                if (_audioBufferLength > MaxAudioBufferSize)
                                {
                                    _loggingService.Info("Audio buffer is full");
                                    _audioBuffer.Clear();
                                    _audioBufferLength = 0;
                                }

                                Buffer.BlockCopy(packetBuffer, 0, bytesToSend, 0, bytesRead);
                            }

                            _audioBuffer.Enqueue(bytesToSend);
                            _audioBufferLength += bytesRead;
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }

                client.Close();
            }

            _loggingService.Info("_audioReceiver finished");
        }

        private void RestartAudio()
        {
            _loggingService.Info("RestartAudio");

            if (_audioPlayer.IsBusy)
            {
                _loggingService.Info("Stopping _audioPlayer");

                _startAudioPlayerThread = true;
                _audioPlayer.CancelAsync();

                lock (_lock)
                {
                    _audioBuffer.Clear();
                }
            }
            else
            {
                _audioPlayer.RunWorkerAsync();
            }

            if (_audioReceiver.IsBusy)
            {
                _loggingService.Info("Stopping _audioReceiver");

                _startAudioReceiverThread = true;
                _audioReceiver.CancelAsync();
            }
            else
            {
                _audioReceiver.RunWorkerAsync();
            }

        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _loggingService.Error(e.ExceptionObject as Exception);
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

                    RestartAudio();
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new DriverInitializationFailedMessage(new DriverInitializationFailedResult()
                    {
                        ErrorId = data == null ? -1 : data.GetIntExtra("marto.rtl_tcp_andro.RtlTcpExceptionId", -1),
                        ExceptionCode = data == null ? 0 : data.GetIntExtra("detailed_exception_code", 0),
                        DetailedDescription = data == null ? "unknown" : data.GetStringExtra("detailed_exception_message")
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