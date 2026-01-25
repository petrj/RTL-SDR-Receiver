using System;
using System.Collections.Generic;
using Terminal.Gui;
using NStack;
using LoggerService;
using RTLSDR.Audio;
using RTLSDR;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RadI0
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var loggingService = new NLogLoggingService( Path.Combine(appPath,"NLog.config"));

            AppDomain.CurrentDomain.UnhandledException += (s,e) =>
            {
                loggingService.Error(e.ExceptionObject as Exception);
            };

            IRawAudioPlayer rawAudioPlayer = null;

            var usingVLC = false;
            if (args != null && args.Length>0)
            {
                if (args.Contains("-vlc", StringComparer.OrdinalIgnoreCase))
                {
                    usingVLC = true;
                    rawAudioPlayer = new VLCSoundAudioPlayer();                     // Linux + Windows
                }
            }

            if (!usingVLC)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    rawAudioPlayer = new NAudioRawAudioPlayer(loggingService);       // Windows only
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    rawAudioPlayer = new AlsaSoundAudioPlayer();                     // Linux only
                }
                else
                {
                    // unsupported platform
                    rawAudioPlayer =  new NoAudioRawAudioPlayer();                    // dummy interface
                }
            }

            var sdrDriver = new RTLSDRPCDriver(loggingService);

            var gui = new RadI0GUI();
            var app = new RadI0App(rawAudioPlayer,sdrDriver,loggingService,gui);
            Task.Run(async () =>
            {
                await app.StartAsync(args);
            });

            gui.OnQuit+= delegate
            {
                rawAudioPlayer.Stop();
            };

            gui.Run();
        }

    }
}
