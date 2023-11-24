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

            var IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData .Length, - 127);
            var lowPassedData = demodulator.LowPass(IQDataSinged16Bit, 96000);  // 107000

            #region mono

                var demodulatedDataMono = demodulator.FMDemodulate(lowPassedData);
                logger.Info($"Demodulated data length: {demodulatedDataMono.Length / 1000} kb");

                WriteDataToFile(sourceFileName + ".fm", demodulatedDataMono);

                var bytesPerOneSec = 96000;
                var savedTime = demodulatedDataMono.Length / bytesPerOneSec;
                logger.Info($"Demodulated data duration: {savedTime} sec");

            #endregion

            #region stereo

            var demodulatedDataStereo = FMDemodulator.DemodulateStereo(lowPassedData, 96000);

                logger.Info($"Demodulated data length: {demodulatedDataStereo.Length / 1000} kb");

                WriteDataToFile(sourceFileName + ".fms", demodulatedDataStereo);

            #endregion

            #region mono with deemph:

                var IQDataSinged16Bit2 = FMDemodulator.Move(IQData, IQData.Length, -127);
                var lowPassedData2 = demodulator.LowPass(IQDataSinged16Bit2, 170000);
                var demodulatedDataMono2 = demodulator.FMDemodulate(lowPassedData2, true);
                var deemphData = demodulator.DeemphFilter(demodulatedDataMono2, 170000);
                var final = demodulator.LowPassReal(deemphData, 170000, 32000);

                WriteDataToFile(sourceFileName + ".fm2", final);

            #endregion
        }

        private static void WriteDataToFile(string fileName, short[] data)
        {
            var bytes = FMDemodulator.ToByteArray(data);

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            File.WriteAllBytes(fileName, bytes);
        }
    }
}
