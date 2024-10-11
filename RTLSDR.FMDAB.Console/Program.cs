using LoggerService;
using RTLSDR.DAB;
using RTLSDR.FM;
using RTLSDR;
using System;
using System.IO;
using RTLSDR.Audio;
using RTLSDR.Common;
using Microsoft.VisualBasic;
using NLog;
using System.Diagnostics;
using System.Threading;

namespace RTLSDR.FMDAB.Console
{
    internal class Program
    {
        private static ConsoleApp _app;
        private static IRawAudioPlayer _audioPlayer;

        private static ILoggingService _loggingService;

        private static void Main(string[] args)
        {
            _app = new ConsoleApp("RTLSDR.FMDAB.Console.x64.exe");
            _app.OnDemodulated += Program_OnDemodulated;

            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            _loggingService = new NLogLoggingService( Path.Combine(appPath,"NLog.config"));

            _app.Run(args);

            System.Console.Write("Press ENTER to exit");
            System.Console.ReadLine();

            _loggingService.Debug("Exiting app");
            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
            }

            _app.Stop();
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
    }
}

