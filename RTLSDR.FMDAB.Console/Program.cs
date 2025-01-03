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
using System.Runtime.InteropServices;

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

            IRawAudioPlayer rawAudioPlayer;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                rawAudioPlayer = new AlsaSoundAudioPlayer();                
            } else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                rawAudioPlayer = new NAudioRawAudioPlayer(loggingService);
            } else
            {
                rawAudioPlayer = new NoAudioRawAudioPlayer();
            }

            var sdrDriver = new RTLTCPIPDriver(loggingService);

            var app = new ConsoleApp(rawAudioPlayer, sdrDriver, loggingService);
            app.Run(args);
        }
    }
}

