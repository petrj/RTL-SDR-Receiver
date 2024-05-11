using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LoggerService;
using RTLSDR;
using RTLSDR.Core;
using RTLSDR.FM;
using RTLSDR.DAB;
using System.Media;
using System.Runtime.InteropServices;

namespace RTLSDRConsole
{
    public class MainClass
    {
        public static ILoggingService logger = new NLogLoggingService("NLog.config");
        private static Stream _outputStream = null;
        private static AppParams _appParams = new AppParams();
        private static int _totalDemodulatedDataLength = 0;
        private static DateTime _demodStartTime;
        private static IDemodulator _demodulator = null;
        private static Stream _stdOut = null;

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            Debug.WriteLine((e.ExceptionObject as Exception).Message);
        }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (_appParams.ParseArgs(args))
            {
                return;
            }

            logger.Info("RTL SDR Test Console");

            // test:
            //var aacDecoder = new AACDecoder(logger);
            //var decodeTest = aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");

            _outputStream = new FileStream(_appParams.OutputFileName, FileMode.Create, FileAccess.Write);

            if (_appParams.FM)
            {
                var fm = new FMDemodulator(logger);
                fm.Emphasize = _appParams.Emphasize;

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

            Console.WriteLine("PRESS any key to exit");
            Console.ReadKey();

        }

        static void Program_OnFinished(object sender, EventArgs e)
        {
            if (_demodulator is DABProcessor dab)
            {
                foreach (var service in dab.FIC.Services)
                {
                    logger.Info($"{Environment.NewLine}{service}");
                }

                dab.StopThreads();
                dab.Stat(true);
            }

            _outputStream.Flush();
            _outputStream.Close();

            logger.Info($"Saved to                     : {_appParams.OutputFileName}");
            logger.Info($"Total demodulated data size  : {_totalDemodulatedDataLength} bytes");
        }

        private static void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                _totalDemodulatedDataLength += ed.Data.Length;
                _outputStream.Write(ed.Data, 0, ed.Data.Length);
            }
        }
    }
}
