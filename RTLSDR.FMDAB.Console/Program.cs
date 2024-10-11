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
        private static void Main(string[] args)
        {
            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var loggingService = new NLogLoggingService( Path.Combine(appPath,"NLog.config"));

            AppDomain.CurrentDomain.UnhandledException += (s,e) =>
            {
                loggingService.Error(e.ExceptionObject as Exception);
            };

#if OS_LINUX
            var audioPlayer = new AlsaSoundAudioPlayer();
            //var audioPlayer = new VLCSoundAudioPlayer();
#elif OS_WINDOWS64
            var audioPlayer = new NAudioRawAudioPlayer(null);
            //var audioPlayer = new VLCSoundAudioPlayer();
#else
            var audioPlayer = new NoAudioRawAudioPlayer();
#endif

            var sdrDriver = new RTLTCPIPDriver(loggingService);

            var app = new ConsoleApp(audioPlayer, sdrDriver, loggingService);
            app.Run(args);
        }
    }
}

