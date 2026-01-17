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
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))

            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var loggingService = new NLogLoggingService( Path.Combine(appPath,"NLog.config"));

            AppDomain.CurrentDomain.UnhandledException += (s,e) =>
            {
                loggingService.Error(e.ExceptionObject as Exception);
            };

            var rawAudioPlayer = new VLCSoundAudioPlayer();                     // Linux + Windows

            //var rawAudioPlayer = new AlsaSoundAudioPlayer();                     // Linux only
            // rawAudioPlayer = new NAudioRawAudioPlayer(loggingService);       // Windows only
            // rawAudioPlayer = new NoAudioRawAudioPlayer();                   // dummy interface

            var sdrDriver = new RTLSDRPCDriver(loggingService);
            var app = new ConsoleApp(rawAudioPlayer, sdrDriver, loggingService);
            app.Run(args);
        }
    }
}

