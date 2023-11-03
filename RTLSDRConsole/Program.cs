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

            //var sourceFileName = "/temp/iq.raw";
            //var targetFileName = "/temp/fm.raw";

            var sourceFileName = @"c:\temp\iq.raw";
            var targetFileName = @"c:\temp\fm.raw";

            var demodulator = new FMDemodulator();

            //var s = System.IO.Path.DirectorySeparatorChar;
            var IQData = File.ReadAllBytes(sourceFileName);

            logger.Info($"Total bytes : {IQData.Length}");
            logger.Info($"Total kbytes: {IQData.Length / 1000}");

            var IQDataSinged16Bit = FMDemodulator.Move(IQData, IQData .Length, - 127);

            var lowPassedData = demodulator.LowPass(IQDataSinged16Bit, 96000);

            logger.Info($"Lowpassed data length: {lowPassedData.Length / 1000} kb");

            var demodulatedData = demodulator.FMDemodulate(lowPassedData);

            logger.Info($"Demodulated data length: {demodulatedData.Length / 1000} kb");

            if (File.Exists(targetFileName))
            {
                File.Delete(targetFileName);
            }

            var demodulatedBytes = FMDemodulator.ToByteArray(demodulatedData);

            File.WriteAllBytes(targetFileName, demodulatedBytes);
        }
    }
}
