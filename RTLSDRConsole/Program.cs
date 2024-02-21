using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LoggerService;
using RTLSDR;
using RTLSDR.Core;
using RTLSDR.FM;
using RTLSDR.DAB;

namespace RTLSDRConsole
{
    public class MainClass
    {
        public static ILoggingService logger = new NLogLoggingService("NLog.config");
        private static Stream _outputStream = null;
        private static AppParams _appParams;
        private static int _totalDemodulatedDataLength = 0;
        private static DateTime _demodStartTime;
        private static IDemodulator _demodulator = null;

        public static bool ParseArgs(string[] args)
        {
            if (args.Length == 0)
            {
                ShowError("No param specified");
                return true;
            }

            foreach (var arg in args)
            {
                var p = arg.ToLower();
                if (p.StartsWith("--", StringComparison.InvariantCulture))
                {
                    p = p.Substring(1);
                }

                if (p == "-help")
                {
                    _appParams.Help = true;
                }
                else
                if (p == "-fm")
                {
                    _appParams.FM = true;
                } else
                if (p == "-dab")
                {
                    _appParams.DAB = true;
                } else
                if (p == "-e")
                {
                    _appParams.Emphasize = true;
                } else
                {
                    _appParams.InputFileName = arg;
                }
            }

            if (!_appParams.Help && string.IsNullOrEmpty(_appParams.InputFileName))
            {
                ShowError($"Input file not specified");
                return true;
            }

            if (!_appParams.Help && !File.Exists(_appParams.InputFileName))
            {
                ShowError($"Input file {_appParams.InputFileName} does not exist");
                return true;
            }

            if (_appParams.Help)
            {
                Help();
                return true;
            }

            if (!_appParams.FM && !_appParams.DAB)
            {
                ShowError("Missing param");
                return true;
            }

            return false;
        }

        public static void Help()
        {
            Console.WriteLine("RTLSDRConsole.exe [option] [input file]");
            Console.WriteLine();
            Console.WriteLine("FM/DAB demodulator");
            Console.WriteLine();
            Console.WriteLine(" input file: unsigned 8 bit integers (uint8 or u8) from rtl_sdr");
            Console.WriteLine();
            Console.WriteLine(" options: ");
            Console.WriteLine(" -fm  \t FM demodulation");
            Console.WriteLine(" -dab \t DAB demodulation");
            Console.WriteLine(" -e   \t emphasize");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("example:");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -fm file.iq:");
            Console.WriteLine(" -> output is raw mono 16bit file.iq.output");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -dab file.iq:");
            Console.WriteLine(" -> output ???");
        }

        public static void ShowError(string text)
        {
            Console.WriteLine($"Error. {text}. See help:");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -help");
            Console.WriteLine();
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            Debug.WriteLine((e.ExceptionObject as Exception).Message);
        }

        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            logger.Info("RTL SDR Test Console");

            if (ParseArgs(args))
            {
                return;
            }

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
                    Bitrate = 96
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
                            logger.Info($"{new string('*', Convert.ToInt32(percents / 2))} {percents.ToString("N2")} %");
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

                dab.Stat();
                dab.StopThreads();
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
                _totalDemodulatedDataLength += ed.Data.Length;
                _outputStream.Write(ed.Data, 0, ed.Data.Length);
            }
        }
    }
}
