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

            //var s = System.IO.Path.DirectorySeparatorChar;
            var IQData = File.ReadAllBytes(sourceFileName);

            var ampCalculator = new AmpCalculation();
            var powerCalculator = new PowerCalculation();

            // last 100 values
            var valuesCount = 100;
            for (var i= IQData.Length-valuesCount*2; i<IQData.Length; i+=2)
            {
                var I = IQData[i + 0] - 127;
                var Q = IQData[i + 1] - 127;

                var a = AmpCalculation.GetAmplitude(I, Q);
                var aPower = 10 * Math.Log(10 * (Math.Pow(I, 2) + Math.Pow(Q, 2)));

                logger.Info($"I: {I.ToString().PadLeft(5,' ')}, Q: {Q.ToString().PadLeft(5, ' ')},  Amplitude : {a.ToString("N2").PadLeft(5,' ')},  power : {a.ToString("N2").PadLeft(10, ' ')} ({aPower.ToString("N2").PadLeft(10, ' ')})");
            }

            logger.Info($"Total bytes : {IQData.Length}");
            logger.Info($"Total kbytes: {IQData.Length / 1000}");

            var power = powerCalculator.GetPowerPercent(IQData, IQData.Length);
            logger.Info($"Power: {power.ToString("N0")} % dBm");

            var IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData .Length, - 127);

            // without deemph:

            var lowPassedData = demodulator.LowPass(IQDataSinged16Bit, 96000);

            logger.Info($"Lowpassed data length: {lowPassedData.Length / 1000} kb");

            var demodulatedData = demodulator.FMDemodulate(lowPassedData, true);

            logger.Info($"Demodulated data length: {demodulatedData.Length / 1000} kb");

            WriteDataToFile(sourceFileName + ".fm", demodulatedData);

            // finding min/max
            var minAmp = short.MaxValue;
            var maxAmp = short.MinValue;
            foreach (var f in demodulatedData)
            {
                if (f > maxAmp)
                {
                    maxAmp = f;
                }
                if (f < minAmp)
                {
                    minAmp = f;
                }
            }

            logger.Info($"Demodulated min/max: {minAmp}/{maxAmp}");

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
