using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LoggerService;
using RTLSDR;

namespace RTLSDRConsole
{
    public struct AppParams
    {
        public bool Help { get; set; }
        public bool FM { get; set; }
        public bool DAB { get; set; }
        public bool Emphemphasize { get; set; }

        public string InputFileName { get; set; }
    }

    public class MainClass
    {
        public static ILoggingService logger = new NLogLoggingService("NLog.config");

        public static AppParams ParseArgs(string[] args)
        {
            var res = new AppParams();

            foreach (var arg in args)
            {
                var p = arg.ToLower();
                if (p.StartsWith("--", StringComparison.InvariantCulture))
                {
                    p = p.Substring(1);
                }

                if (p == "-help")
                {
                    res.Help = true;
                }
                else
                if (p == "-fm")
                {
                    res.FM = true;
                } else
                if (p == "-dab")
                {
                    res.DAB = true;
                } else
                if (p == "-e")
                {
                    res.Emphemphasize = true;
                } else
                {
                    res.InputFileName = arg;
                }
            }

            return res;
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

        public static void Main(string[] args)
        {
            logger.Info("RTL SDR Test Console");

            if (args.Length == 0)
            {
                ShowError("No param specified");
                return;
            }

            var appParams = ParseArgs(args);

            if (!appParams.Help && string.IsNullOrEmpty(appParams.InputFileName))
            {
                ShowError($"Input file not specified");
                return;
            }

            if (!appParams.Help && !File.Exists(appParams.InputFileName))
            {
                ShowError($"Input file {appParams.InputFileName} does not exist");
                return;
            }

            if (appParams.Help)
            {
                Help();
                return;
            } 

            if (!appParams.FM && !appParams.DAB)
            {
                ShowError("Missing param");
                return;
            }

            var bufferSize = 1024 * 1024;
            var IQDataBuffer = new byte[bufferSize];
            var totalDemodulatedDataLength = 0;
            byte[] demodBytes = new byte[0];
            PowerCalculation powerCalculator = null;

            using (var outputFs = new FileStream(appParams.InputFileName + ".output", FileMode.Create, FileAccess.Write))
            {
                using (var inputFs = new FileStream(appParams.InputFileName, FileMode.Open, FileAccess.Read))
                {
                    logger.Info($"Total bytes : {inputFs.Length}");

                    while (inputFs.Position < inputFs.Length)
                    {
                        var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);

                        logger.Info($"{bytesRead} bytes read");

                        if (powerCalculator == null)
                        {
                            powerCalculator = new PowerCalculation();
                            var power = powerCalculator.GetPowerPercent(IQDataBuffer, bytesRead);
                            logger.Info($"Power: {power.ToString("N0")} % dBm");
                        }

                        if (appParams.FM)
                        {
                            if (appParams.Emphemphasize)
                            {
                                demodBytes = FMDemodulateE(IQDataBuffer, bytesRead);
                            }
                            else
                            {
                                demodBytes = FMDemodulate(IQDataBuffer, bytesRead);
                            }
                        }

                        outputFs.Write(demodBytes, 0, demodBytes.Length);

                        totalDemodulatedDataLength += demodBytes.Length;
                    }
                }

                outputFs.Flush();
                outputFs.Close();
            }

            logger.Info($"Total demodulated data size  : {totalDemodulatedDataLength} bytes");

            if (appParams.FM)
            {
                var savedTime = (totalDemodulatedDataLength/2) / (appParams.Emphemphasize ? 32000 : 96000);   // 2 bytes for every second
                logger.Info($"Record time                  : {savedTime} sec");
            }

            logger.Info($"Saved to                     : {appParams.InputFileName + ".output"}");

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

        private static byte[] FMDemodulate(byte[] IQData, int length, int samplerate = 96000)
        {
            var demodulator = new FMDemodulator();

            var demodBuffer = new short[length];

            var lowPassedDataLength = demodulator.LowPassWithMove(IQData, demodBuffer, length, samplerate, -127);

            var demodulatedDataMonoLength = demodulator.FMDemodulate(demodBuffer, lowPassedDataLength, false);

            return GetBytes(demodBuffer, demodulatedDataMonoLength);
        }

        private static byte[] FMDemodulateE(byte[] IQData, int length)
        {
            var demodulator = new FMDemodulator();

            var demodBuffer = new short[length];

            var lowPassedDataMonoDeemphLength = demodulator.LowPassWithMove(IQData, demodBuffer, length, 170000, -127);
            var demodulatedDataMono2Length = demodulator.FMDemodulate(demodBuffer, lowPassedDataMonoDeemphLength, true);
            demodulator.DeemphFilter(demodBuffer, demodulatedDataMono2Length, 170000);
            var finalBytesCount = demodulator.LowPassReal(demodBuffer, demodulatedDataMono2Length, 170000, 32000);

            return GetBytes(demodBuffer, finalBytesCount);
        }

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

        private static void WriteDataToFile(string fileName, short[] data, int count = -1)
        {
            var bytes = FMDemodulator.ToByteArray(data);

            if (count != -1)
            {
                var bytesPart = new byte[count];
                Buffer.BlockCopy(bytes, 0, bytesPart, 0, count);
                bytes = bytesPart;
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            File.WriteAllBytes(fileName, bytes);

        }
    }
}
