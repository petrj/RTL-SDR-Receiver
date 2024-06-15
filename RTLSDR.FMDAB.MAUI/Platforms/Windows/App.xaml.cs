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
        private int _audioChannels;
        private int _streamPort = 1235;
        private Thread _audioThread = null;
        private bool _audioThreadRunning = true;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            WeakReferenceMessenger.Default.Register<InitDriverMessage>(this, (sender, obj) =>
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

            WeakReferenceMessenger.Default.Register<NotifyAudioChangeMessage>(this, (sender, obj) =>
            {
                if (obj.Value is AudioDataDescription desc)
                {
                    _audioSampleRate = desc.SampleRate;
                    _audioChannels = desc.Channels;

                    while (_audioThread != null && _audioThread.ThreadState == ThreadState.Running)
                    {
                        _audioThreadRunning = false;
                        Thread.Sleep(50);
                    };

                    _audioThread = new Thread(AudioLoop);
                    _audioThread.Start();
                }
            });
        }

        private void AudioLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _streamPort);
            using (Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                client.Bind(remoteEP);

                var packetBuffer = new byte[UDPStreamer.MaxPacketSize];

                while (_audioThreadRunning)
                {
                    if (client.Available > 0)
                    {
                        var bytesRead = client.Receive(packetBuffer);

                        //_audioTrack.Write(packetBuffer, 0, bytesRead);
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
                client.Close();
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}