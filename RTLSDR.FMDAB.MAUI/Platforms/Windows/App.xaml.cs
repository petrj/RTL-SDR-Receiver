using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using RTLSDR;
using RTLSDR.Common;
using System.Net.Sockets;
using System.Net;
using System.Speech.AudioFormat;
using Windows.Media.Core;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Windows.Security.Cryptography.Core;
using LoggerService;
using RTLSDR.Audio;
using NLog.Config;
using System.Security.Cryptography;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace RTLSDRReceiver.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        private int _audioSampleRate;
        private short _audioChannels;
        private int _streamPort = 1235;
        private Thread _audioThread = null;
        private bool _audioThreadRunning = true;
        private IRawAudioPlayer _audioPlayer;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            WeakReferenceMessenger.Default.Register<InitTestDriverMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DriverSettings settings)
                {
                    _streamPort = settings.Streamport;
                    WeakReferenceMessenger.Default.Send(new DriverInitializedMessage(new DriverInitializationResult()
                    {
                        //SupportedTcpCommands = data.GetIntArrayExtra("supportedTcpCommands"),
                        DeviceName = "Windows mockup device",
                        OutputRecordingDirectory = "c:\\temp"
                    }));
                }
            });

            WeakReferenceMessenger.Default.Register<InitTCPDriverMessage>(this, (sender, obj) =>
            {
                WeakReferenceMessenger.Default.Send(new DriverInitializedMessage(new DriverInitializationResult()
                {
                    //SupportedTcpCommands = data.GetIntArrayExtra("supportedTcpCommands"),
                    DeviceName = "RTL TCP",
                    OutputRecordingDirectory = "c:\\temp"
                }));
            });


            WeakReferenceMessenger.Default.Register<NotifyAudioChangeMessage>(this, (sender, obj) =>
            {
                if (obj.Value is AudioDataDescription desc)
                {
                    _audioSampleRate = desc.SampleRate;
                    _audioChannels = desc.Channels;

                    StopAudioThread();

                    _audioThread = new Thread(AudioLoop);
                    _audioThread.Start();
                }
            });

            WeakReferenceMessenger.Default.Register<NotifyAudioStopMessage>(this, (sender, obj) =>
            {
                StopAudioThread();
            });
        }

        private void StopAudioThread()
        {
            while (_audioThread != null && _audioThread.ThreadState != System.Threading.ThreadState.Stopped)
            {
                _audioThreadRunning = false;
                Thread.Sleep(50);
            };
        }

        private void AudioLoop()
        {
            var logger = new NLogLoggingService(Path.Join(AppContext.BaseDirectory, "Platforms\\Windows\\NLog.config"));

            _audioPlayer = new NAudioRawAudioPlayer(logger);
            //_audioPlayer = new VLCSoundAudioPlayer();
            _audioPlayer.Init(new AudioDataDescription()
            {
                BitsPerSample = 16,
                Channels = _audioChannels,
                SampleRate = _audioSampleRate,
            }, logger);
            _audioPlayer.Play();

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _streamPort);
            using (Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                client.Bind(remoteEP);

                var packetBuffer = new byte[UDPStreamer.MaxPacketSize];

                _audioThreadRunning = true;

                while (_audioThreadRunning)
                {
                    if (client.Available > 0)
                    {
                        var bytesRead = client.Receive(packetBuffer);

                        if (bytesRead == UDPStreamer.MaxPacketSize)
                        {
                            _audioPlayer.AddPCM(packetBuffer);
                        }
                        else if (bytesRead > 0)
                        {
                            var buf = new byte[bytesRead];
                            Buffer.BlockCopy(packetBuffer, 0, buf, 0, bytesRead);
                            _audioPlayer.AddPCM(buf);
                        }
                    }
                    else
                    {
                        Thread.Sleep(18);
                    }
                }

                _audioPlayer.Stop();
                _audioPlayer = null;
                client.Close();
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();


    }
}