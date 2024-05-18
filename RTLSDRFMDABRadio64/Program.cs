using LoggerService;
using RTLSDR.DAB;
using RTLSDR.Core;
using RTLSDR.FM;
using System;
using System.IO;
using RTLSDR.RTLSDRFMDABRadioConsoleCommon;
//using NAudio.Wave;

internal class Program
{
    private static void Main(string[] args)
    {
        new ConsoleApp("RTLSDRFMDABRadio64").Run(args);
    }

    //// NAudio:
    ////BufferedWaveProvider _bufferedWaveProvider = null;
    ////WaveOut _waveOut = null;

    //ConsoleAppParams _appParams;

    //int _totalDemodulatedDataLength = 0;
    //DateTime _demodStartTime;
    //IDemodulator _demodulator = null;


        //_bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 2))
        //{
        //    BufferLength = 2560 * 32,
        //    DiscardOnBufferOverflow = true
        //};

        //var waveOut = new WaveOut();
        //waveOut.Init(_bufferedWaveProvider);
        //waveOut.Play();


    private void Program_OnDemodulated(object sender, EventArgs e)
    {
        if (e is DataDemodulatedEventArgs ed)
        {
            if (ed.Data == null || ed.Data.Length == 0)
            {
                return;
            }

            //if (_bufferedWaveProvider != null)
            //{
            //    logger.Info($"Writing {ed.Data.Length} to audio buffer...");
            //    _bufferedWaveProvider.AddSamples(ed.Data, 0, ed.Data.Length);
            //}
        }
    }
}