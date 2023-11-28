using System;
using System.Diagnostics;
using System.IO;
using LoggerService;
using RTLSDR;

namespace RTLSDRConsole
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var logger = new NLogLoggingService("NLog.config");
            logger.Info("RTL SDR Test Console");

            if (args.Length == 0)
            {
                logger.Info("No param");
                return;
            }

            var sourceFileName = args[0];

            var demodulator = new FMDemodulator();

            var IQData = File.ReadAllBytes(sourceFileName);

            var powerCalculator = new PowerCalculation();

            logger.Info($"Total bytes : {IQData.Length}");
            logger.Info($"Total kbytes: {IQData.Length / 1000}");

            var power = powerCalculator.GetPowerPercent(IQData, IQData.Length);
            logger.Info($"Power: {power.ToString("N0")} % dBm");

            var beforeMove = DateTime.Now;

            var IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData .Length, - 127);



            var beforeLowPass = DateTime.Now;

            var lowPassedDataLength = demodulator.LowPass(IQDataSinged16Bit, 96000);  // 107000

            #region mono

                var beforeDemod = DateTime.Now;

                var demodulatedDataMonoLength = demodulator.FMDemodulate(IQDataSinged16Bit, lowPassedDataLength, true);

                var afterDemod = DateTime.Now;

                logger.Info($"Move duration                : {(beforeLowPass - beforeMove).TotalMilliseconds.ToString("N2")} ms");
                logger.Info($"LowPass duration             : {(beforeDemod - beforeLowPass).TotalMilliseconds.ToString("N2")} ms");
                logger.Info($"Demodulation duration        : {(afterDemod - beforeDemod).TotalMilliseconds.ToString("N2")} ms");
                logger.Info($"Overall demodulation duration: {(afterDemod - beforeMove).TotalMilliseconds.ToString("N2")} ms");
                logger.Info($"------------------------------------------------");

                logger.Info($"Demodulated data length      : {demodulatedDataMonoLength / 1000} kb");

                var bytesPerOneSec = 96000;
                var savedTime = demodulatedDataMonoLength / bytesPerOneSec;
                logger.Info($"Record time                  : {savedTime} sec");

                WriteDataToFile(sourceFileName + ".fm", IQDataSinged16Bit, demodulatedDataMonoLength);

                logger.Info($"saved to                     : {sourceFileName + ".fm"}");

            #endregion

            #region mono with deemph:

            IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData.Length, -127);

                var lowPassedDataMonoDeemphLength = demodulator.LowPass(IQDataSinged16Bit, 170000);
                var demodulatedDataMono2Length = demodulator.FMDemodulate(IQDataSinged16Bit, lowPassedDataMonoDeemphLength, true);
                demodulator.DeemphFilter(IQDataSinged16Bit, demodulatedDataMono2Length, 170000);
                var finalBytesCount = demodulator.LowPassReal(IQDataSinged16Bit, demodulatedDataMono2Length, 170000, 32000);

                WriteDataToFile(sourceFileName + ".fm2", IQDataSinged16Bit, finalBytesCount);

            #endregion

            /*

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
