using LoggerService;
using RTLSDR.DAB;
using RTLSDR.Core;
using RTLSDR.FM;
using System;
using System.IO;
using NAudio.Wave;

internal class Program
{
    private static void Main(string[] args)
    {
        new MainClass().Run(args);
    }
}

public class MainClass
{
    ILoggingService logger = new NLogLoggingService(System.IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory,"NLog.config"));

    Stream _outputFileStream = null;
    Stream _stdOut = null;

    // NAudio:
    //BufferedWaveProvider _bufferedWaveProvider = null;
    //WaveOut _waveOut = null;

    ConsoleAppParams _appParams;

    int _totalDemodulatedDataLength = 0;
    DateTime _demodStartTime;
    IDemodulator _demodulator = null;

    bool _fileProcessed = false;

    public void Run(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        logger.Info("DAB+/FM Console Radio Player");

        _appParams = new ConsoleAppParams("RTLSDRFMDABRadio64.exe");
        if (_appParams.ParseArgs(args))
        {
            return;
        }

        // test:
        //var aacDecoder = new AACDecoder(logger);
        //aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");

        _outputFileStream = new FileStream(_appParams.OutputFileName, FileMode.Create, FileAccess.Write);

        if (_appParams.StdOut)
        {
            _stdOut = Console.OpenStandardOutput();
        }

        if (_appParams.FM)
        {
            var fm = new FMDemodulator(logger);
            fm.Emphasize = _appParams.FMEmphasize;

            _demodulator = fm;
        }
        if (_appParams.DAB)
        {
            var DABProcessor = new DABProcessor(logger);
            DABProcessor.ProcessingSubChannel = new DABSubChannel()
            {
                StartAddr = 570,
                Length = 72, // 90
                Bitrate = 96,
                ProtectionLevel =  EEPProtectionLevel.EEP_3
            };
            _demodulator = DABProcessor;
        }

        _demodulator.OnDemodulated += Program_OnDemodulated;
        _demodulator.OnFinished += Program_OnFinished;

        var bufferSize = 1024 * 1024;
        var IQDataBuffer = new byte[bufferSize];

        PowerCalculation powerCalculator = null;

        //_bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 2))
        //{
        //    BufferLength = 2560 * 32,
        //    DiscardOnBufferOverflow = true
        //};

        //var waveOut = new WaveOut();
        //waveOut.Init(_bufferedWaveProvider);
        //waveOut.Play();

        _demodStartTime = DateTime.Now;
            var lastBufferFillNotify = DateTime.MinValue;

            using (var inputFs = new FileStream(_appParams.InputFileName, FileMode.Open, FileAccess.Read))
            {
                logger.Info($"Total bytes : {inputFs.Length}");
                long totalBytesRead = 0;

                while (inputFs.Position <  inputFs.Length)
                {
                    var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);
                    totalBytesRead += bytesRead;

                    if ((DateTime.Now - lastBufferFillNotify).TotalMilliseconds > 500)
                    {
                        lastBufferFillNotify = DateTime.Now;
                        if (inputFs.Length > 0)
                        {
                            var percents = (totalBytesRead / (inputFs.Length / 100));
                            logger.Debug($" Processing input file:                   {percents} %");
                        }
                    }

                    if (powerCalculator == null)
                    {
                        powerCalculator = new PowerCalculation();
                        var power = powerCalculator.GetPowerPercent(IQDataBuffer, bytesRead);
                        logger.Info($"Power: {power.ToString("N0")} % dBm");
                    }

                    _demodulator.AddSamples(IQDataBuffer, bytesRead);

                    System.Threading.Thread.Sleep(200);
                }
            }

            _demodulator.Finish();

            while (!_fileProcessed)
            {
                System.Threading.Thread.Sleep(500);
            }
    }

    private void Program_OnDemodulated(object sender, EventArgs e)
    {
        if (e is DataDemodulatedEventArgs ed)
        {
            if (ed.Data == null || ed.Data.Length == 0)
            {
                return;
            }

            _totalDemodulatedDataLength += ed.Data.Length;
            _outputFileStream.Write(ed.Data, 0, ed.Data.Length);

            //if (_bufferedWaveProvider != null)
            //{
            //    logger.Info($"Writing {ed.Data.Length} to audio buffer...");
            //    _bufferedWaveProvider.AddSamples(ed.Data, 0, ed.Data.Length);
            //}

            if (_stdOut != null)
            {
                _stdOut.Write(ed.Data, 0, ed.Data.Length);
            }
        }
    }

    private void Program_OnFinished(object sender, EventArgs e)
    {
        _fileProcessed = true;

        if (_demodulator is DABProcessor dab)
        {
            foreach (var service in dab.FIC.Services)
            {
                logger.Info($"{Environment.NewLine}{service}");
            }

            dab.StopThreads();
            dab.Stat(true);
        }

        //if (_waveOut != null)
        //{
        //    _waveOut.Stop();
        //}

        if (_stdOut != null)
        {
            _stdOut.Flush();
            _stdOut.Close();
            _stdOut.Dispose();
        }

        _outputFileStream.Flush();
        _outputFileStream.Close();
        _outputFileStream.Dispose();

        logger.Info($"Saved to                     : {_appParams.OutputFileName}");
        logger.Info($"Total demodulated data size  : {_totalDemodulatedDataLength} bytes");
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.WriteLine((e.ExceptionObject as Exception).Message);
        logger.Error(e.ExceptionObject as Exception);
    }

}
