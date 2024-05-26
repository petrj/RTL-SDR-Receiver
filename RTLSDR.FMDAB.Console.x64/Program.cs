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
        private static ConsoleApp _app;
        private static IRawAudioPlayer _audioPlayer;

        private static void Main(string[] args)
        {
            _app = new ConsoleApp("RTLSDR.FMDAB.Console.x64.exe");
            _app.OnDemodulated += Program_OnDemodulated;
            _app.OnFinished += App_OnFinished;

            _app.Run(args);
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
                            //_audioPlayer = new NAudioRawAudioPlayer();
                            //_audioPlayer = new NoAudioRawAudioPlayer();
                            _audioPlayer = new LinuxRawAudioPlayer();
                            _audioPlayer.Init(ed.AudioDescription);
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
            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
            }
        }
    }
}

