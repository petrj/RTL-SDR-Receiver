using System;
using System.Collections.Generic;
using Terminal.Gui;
using NStack;
using LoggerService;
using RTLSDR.Audio;
using RTLSDR;

namespace Rad10
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

            var rawAudioPlayer = new VLCSoundAudioPlayer();                     // Linux + Windows

            //var rawAudioPlayer = new AlsaSoundAudioPlayer();                     // Linux only
            // rawAudioPlayer = new NAudioRawAudioPlayer(loggingService);       // Windows only
            // rawAudioPlayer = new NoAudioRawAudioPlayer();                   // dummy interface

            var sdrDriver = new RTLSDRPCDriver(loggingService);       

            var gui = new Rad10GUI();
            var app = new Rad10App(rawAudioPlayer,sdrDriver,loggingService,gui);            
            Task.Run(async () => await app.StartAsync(args));

            gui.Run();
        }        
    }
}
