using LoggerService;
using RTLSDR.DAB;
using RTLSDR.FM;
using RTLSDR;
using System;
using System.IO;
using RTLSDR.FMDAB.Console.Common;
using RTLSDR.Audio;
using RTLSDR.Common;
using Microsoft.VisualBasic;
using NLog;
using System.Diagnostics;
using System.Threading;

namespace RTLSDR.FMDAB.Console.x64
{
    internal class Program
    {
        private static ConsoleApp _app;
        private static IRawAudioPlayer _audioPlayer;

        private static ILoggingService _loggingService;

        private static ISDR _sdrDriver = null;

        private static void Main(string[] args)
        {
            _app = new ConsoleApp("RTLSDR.FMDAB.Console.x64.exe");
            _app.OnDemodulated += Program_OnDemodulated;
            _app.OnFinished += App_OnFinished;

            _loggingService = new BasicLoggingService();

            _sdrDriver = new RTLTCPIPDriver(_loggingService);

            _app.Run(args);

            System.Console.Write("Press ENTER to exit");
            System.Console.ReadLine();

            _loggingService.Debug("Exiting app");
            if (_audioPlayer != null)
            {
             _audioPlayer.Stop();
            }System.Console.Write("Press ENTER to exit");System.Console.Write("Press ENTER to exit");System.Console.Write("Press ENTER to exit");System.Console.Write("Press ENTER to exit");
            _app.Stop();
            _sdrDriver.Disconnect();
        }

        private static void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                try
                {
                    if (_app.Params.Play)
                    {
                        if (_audioPlayer == null)
                        {
#if OS_LINUX
                            _audioPlayer = new AlsaSoundAudioPlayer();
                            //_audioPlayer = new VLCSoundAudioPlayer();
#elif OS_WINDOWS64
                            _audioPlayer = new NAudioRawAudioPlayer(null);
                            //_audioPlayer = new VLCSoundAudioPlayer();
#else
                            _audioPlayer = new NoAudioRawAudioPlayer();
#endif
                            _audioPlayer.Init(ed.AudioDescription, _loggingService);
                            _audioPlayer.Play();
                        }

                        _audioPlayer.AddPCM(ed.Data);
                    }
                } catch (Exception ex)
                {
                    _app.Logger.Error(ex);
                }
            }
        }

        private static void App_OnFinished(object? sender, EventArgs e)
        {

        }
    }
}

