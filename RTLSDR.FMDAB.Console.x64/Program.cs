using LoggerService;
using RTLSDR.DAB;
using RTLSDR.FM;
using System;
using System.IO;
using RTLSDR.FMDAB.Console.Common;
using RTLSDR.Common;
using Microsoft.VisualBasic;
using NAudio.Wave;

namespace RTLSDR.FMDAB.Console.x64
{
    internal class Program
    {

        public static WaveOutEvent _outputDevice;
        public static BufferedWaveProvider _bufferedWaveProvider;

        private static void Main(string[] args)
        {
            _outputDevice = new WaveOutEvent();
            var waveFormat = new WaveFormat(48000, 16, 2);
            _bufferedWaveProvider = new BufferedWaveProvider(waveFormat);
            _outputDevice.Init(_bufferedWaveProvider);
            _outputDevice.Play();

            var app = new ConsoleApp("RTLSDR.FMDAB.Console.x64");
            app.OnDemodulated += Program_OnDemodulated;
            app.OnFinished += App_OnFinished;
            app.Run(args);
        }

        private static void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                if (_bufferedWaveProvider != null)
                {
                    _bufferedWaveProvider.AddSamples(ed.Data, 0, ed.Data.Length);
                }
            }
        }

        private static void App_OnFinished(object? sender, EventArgs e)
        {
            _outputDevice.Stop();
            _bufferedWaveProvider.ClearBuffer();
        }
    }
}
