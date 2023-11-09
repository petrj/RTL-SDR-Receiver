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


            for (var i=0;i<IQData.Length/2;i++)
            {
                var angle = AmpCalculation.GetPhaseAngle(IQData[i * 2 + 0] - 127, IQData[i * 2 + 1] - 127);
                angle = angle * 180 / Math.PI;
                //logger.Info($"Amplitude : {a.ToString("N2").PadLeft(30,' ')}, Phase angle: {angle.ToString("N2").PadLeft(30,' ')}");
            }

            logger.Info($"Total bytes : {IQData.Length}");
            logger.Info($"Total kbytes: {IQData.Length / 1000}");

            // last sample amplitude:

            var amplitude = AmpCalculation.GetAmplitude(IQData[IQData.Length -2], IQData[IQData.Length - 1]);
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
