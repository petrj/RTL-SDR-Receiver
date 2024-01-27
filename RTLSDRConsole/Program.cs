using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LoggerService;
using RTLSDR;
using DAB;
using SDRLib;

namespace RTLSDRConsole
{
    public struct AppParams
    {
        public bool Help { get; set; }
        public bool FM { get; set; }
        public bool DAB { get; set; }
        public bool Emphasize { get; set; }

        public string InputFileName { get; set; }

        public string OutputFileName
        {
            get
            {
                var res = InputFileName;
                if (FM)
                {
                    res += ".fm";
                }
                if (DAB)
                {
                    res += ".dab";
                }
                return res;
            }
        }
    }

    public class MainClass
    {
        public static ILoggingService logger = new NLogLoggingService("NLog.config");
        private static Stream _outputStream = null;
        private static AppParams _appParams;
        private static int _totalDemodulatedDataLength = 0;

        public static void ParseArgs(string[] args)
        {
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
            //var dab = new DABMainClass();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            logger.Info("RTL SDR Test Console");

            if (args.Length == 0)
            {
                ShowError("No param specified");
                return;
            }

            ParseArgs(args);

            if (!_appParams.Help && string.IsNullOrEmpty(_appParams.InputFileName))
            {
                ShowError($"Input file not specified");
                return;
            }

            if (!_appParams.Help && !File.Exists(_appParams.InputFileName))
            {
                ShowError($"Input file {_appParams.InputFileName} does not exist");
                return;
            }

            if (_appParams.Help)
            {
                Help();
                return;
            }

            if (!_appParams.FM && !_appParams.DAB)
            {
                ShowError("Missing param");
                return;
            }

            _outputStream = new FileStream(_appParams.OutputFileName, FileMode.Create, FileAccess.Write);

            var fm = new FM.FMDemodulator(logger);
            fm.OnDemodulated += Fm_OnDemodulated;
            fm.Emphasize = _appParams.Emphasize;

            var bufferSize = 1024 * 1024;
            var IQDataBuffer = new byte[bufferSize];

            //byte[] demodBytes = new byte[0];

            PowerCalculation powerCalculator = null;

            var DAB = new DABProcessor(logger);
            DAB.ProcessingSubChannel = new DABSubChannel()
            {
                 StartAddr = 570,
                 Length = 90 //72
            };
            DAB.DumpFileName = _appParams.InputFileName + ".dab";

            using (var inputFs = new FileStream(_appParams.InputFileName, FileMode.Open, FileAccess.Read))
            {
                logger.Info($"Total bytes : {inputFs.Length}");
                long totalBytesRead = 0;

                while (inputFs.Position <  inputFs.Length)
                {
                    var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);
                    totalBytesRead += bytesRead;

                    if (inputFs.Length > 0)
                    {
                        logger.Info($"--> : {(totalBytesRead/1024).ToString("N0")}/{(inputFs.Length/1000).ToString("N0")} KB ({ (totalBytesRead / (inputFs.Length / 100)).ToString("N2")} %)");
                    }

                    if (powerCalculator == null)
                    {
                        powerCalculator = new PowerCalculation();
                        var power = powerCalculator.GetPowerPercent(IQDataBuffer, bytesRead);
                        logger.Info($"Power: {power.ToString("N0")} % dBm");
                    }

                    if (_appParams.FM)
                    {
                        fm.AddSamples(IQDataBuffer, bytesRead);

                        /*
                        if (appParams.Emphemphasize)
                        {
                            demodBytes = FMDemodulateE(IQDataBuffer, bytesRead);
                        }
                        else
                        {
                            demodBytes = FMDemodulate(IQDataBuffer, bytesRead);
                        }

                        outputFs.Write(demodBytes, 0, demodBytes.Length);
                        */                       
                    }

                    if (_appParams.DAB)
                    {
                        DAB.AddSamples(IQDataBuffer, bytesRead);
                    }

                    System.Threading.Thread.Sleep(200);
                }
            }

            if (_appParams.FM)
            {
                fm.Finish();
                //var savedTime = (totalDemodulatedDataLength/2) / (_appParams.Emphemphasize ? 32000 : 96000);   // 2 bytes for every second
                //logger.Info($"Record time                  : {savedTime} sec");
            }

            _outputStream.Flush();
            _outputStream.Close();

            logger.Info($"Saved to                     : {_appParams.OutputFileName}");
            logger.Info($"Total demodulated data size  : {_totalDemodulatedDataLength} bytes");

            Console.WriteLine("PRESS any key to exit");
            Console.ReadKey();

            /*
            #region mono with deemph:

            #region stereo

            var demodulatedDataStereo = FMDemodulator.DemodulateStereo(lowPassedData, 96000);

                logger.Info($"Demodulated data length: {demodulatedDataStereo.Length / 1000} kb");

                WriteDataToFile(sourceFileName + ".fms", demodulatedDataStereo);

            #endregion

            #region stereo with Deemph

                IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData.Length, -127);
                var lowPassedDataStereoDeemph = demodulator.LowPass(IQDataSinged16Bit, 170000);

                var demodulatedDataStereoDeemph = FMDemodulator.DemodulateStereoDeemph(lowPassedDataStereoDeemph, 170000, 32000);

                logger.Info($"Demodulated data length: {demodulatedDataStereoDeemph.Length / 1000} kb");

                WriteDataToFile(sourceFileName + ".fms2", demodulatedDataStereoDeemph);

            #endregion
            */
        }

        private static void Fm_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                _totalDemodulatedDataLength += ed.Data.Length;
                _outputStream.Write(ed.Data, 0, ed.Data.Length);
            }
        }

/*
        private static byte[] FMDemodulate(byte[] IQData, int length, int samplerate = 96000)
        {
            var demodBuffer = new short[length];

            var lowPassedDataLength = _demodulator.LowPassWithMove(IQData, demodBuffer, length, samplerate, -127);

            var demodulatedDataMonoLength = _demodulator.FMDemodulate(demodBuffer, lowPassedDataLength, false);

            return GetBytes(demodBuffer, demodulatedDataMonoLength);
        }

        private static byte[] FMDemodulateE(byte[] IQData, int length)
        {
            var demodBuffer = new short[length];

            var lowPassedDataMonoDeemphLength = _demodulator.LowPassWithMove(IQData, demodBuffer, length, 170000, -127);
            var demodulatedDataMono2Length = _demodulator.FMDemodulate(demodBuffer, lowPassedDataMonoDeemphLength, true);
            _demodulator.DeemphFilter(demodBuffer, demodulatedDataMono2Length, 170000);
            var finalBytesCount = _demodulator.LowPassReal(demodBuffer, demodulatedDataMono2Length, 170000, 32000);

            return GetBytes(demodBuffer, finalBytesCount);
        }
*/        

        private static byte[] GetBytes(short[] data, int count)
        {
            var res = new byte[count * 2];

            var pos = 0;
            for (int i = 0; i < count; i++)
            {
                var dataToWrite = BitConverter.GetBytes(data[i]);
                res[pos + 0] = (byte)dataToWrite[0];
                res[pos + 1] = (byte)dataToWrite[1];
                pos += 2;
            }

            return res;
        }

      
    }
}
