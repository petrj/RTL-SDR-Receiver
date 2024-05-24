using LoggerService;
using RTLSDR.DAB;
using RTLSDR.FM;
using System;
using System.IO;
using RTLSDR.FMDAB.Console.Common;
using RTLSDR.Audio;
using RTLSDR.Common;
using Microsoft.VisualBasic;
using NLog;
using System.Diagnostics;

namespace RTLSDR.FMDAB.Console.x64
{
    internal class Program
    {
        private static IRawAudioPlayer _audioPlayer;

        private static void Main(string[] args)
        {
            var app = new ConsoleApp("RTLSDR.FMDAB.Console.x64.exe");
            app.OnDemodulated += Program_OnDemodulated;
            app.OnFinished += App_OnFinished;
            app.Run(args);

            //_audioPlayer = new NAudioRawAudioPlayer();
            //_audioPlayer = new NoAudioRawAudioPlayer();
            _audioPlayer = new LinuxRawAudioPlayer();
            _audioPlayer.Init();
            _audioPlayer.Play();
        }

        private static void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                if (_audioPlayer != null)
                {
                    _audioPlayer.AddPCM(ed.Data);
                }
            }
        }

        private static void App_OnFinished(object? sender, EventArgs e)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
            }
        }
    }
}

