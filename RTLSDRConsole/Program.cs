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
            var logger = new BasicLoggingService();
            logger.Info("RTL SDR Test Console");

            if (args.Length == 0)
            {
                logger.Info("No param");
                return;
            }

            var sourceFileName = args[0];

            var demodulator = new FMDemodulator();

            //var s = System.IO.Path.DirectorySeparatorChar;
            var IQData = File.ReadAllBytes(sourceFileName);

            // show last 1000 values
            for (var i= IQData.Length - 1000; i<IQData.Length;i+=2)
            {
                var I = IQData[i + 0] - 127;
                var Q = IQData[i + 1] - 127;

                var a = AmpCalculation.GetAmplitude(I, Q);
                var aPercent = a / (AmpCalculation.AmpMax / 100);
                logger.Info($"I: {I.ToString().PadLeft(10,' ')}, Q: {Q.ToString().PadLeft(10, ' ')},  Amplitude : {a.ToString("N2").PadLeft(20,' ')} ({aPercent.ToString("N2").PadLeft(20, ' ')} %)");
            }

            logger.Info($"Total bytes : {IQData.Length}");
            logger.Info($"Total kbytes: {IQData.Length / 1000}");

            // last sample amplitude:

            var amplitude = AmpCalculation.GetAmplitude(IQData[IQData.Length -2] - 127, IQData[IQData.Length - 1] - 127);
            var amplitudePercent = amplitude / (AmpCalculation.AmpMax / 100);

            logger.Info($"Percent signal: {amplitudePercent.ToString("N0")} %");

            var IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData .Length, - 127);

            // without deemph:

            var lowPassedData = demodulator.LowPass(IQDataSinged16Bit, 96000);

            logger.Info($"Lowpassed data length: {lowPassedData.Length / 1000} kb");

            var demodulatedData = demodulator.FMDemodulate(lowPassedData);

            logger.Info($"Demodulated data length: {demodulatedData.Length / 1000} kb");

            WriteDataToFile(sourceFileName + ".fm", demodulatedData);

            // with deemph:

            lowPassedData = demodulator.LowPass(IQDataSinged16Bit, 170000);

            logger.Info($"Lowpassed data length: {lowPassedData.Length / 1000} kb");

            demodulatedData = demodulator.FMDemodulate(lowPassedData);

            var deemphData = demodulator.DeemphFilter(demodulatedData, 170000);
            var final = demodulator.LowPassReal(deemphData, 170000, 32000);

            WriteDataToFile(sourceFileName + ".fm2", final);
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
