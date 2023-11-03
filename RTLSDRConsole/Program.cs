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

            //var s = System.IO.Path.DirectorySeparatorChar;
            var IQData = File.ReadAllBytes(@"/temp/iq.raw");

            logger.Info($"Total bytes : {IQData.Length}");
            logger.Info($"Total kbytes: {IQData.Length / 1000}");

            var lowPassedData = FMDemodulator.LowPass(IQData, 96000);

            logger.Info($"Lowpassed data length: {lowPassedData.Length / 1000} kb");

            var demodulatedData = FMDemodulator.FMDemodulate(lowPassedData);

            logger.Info($"Demodulated data length: {demodulatedData.Length / 1000} kb");


            var targetFileName = "/temp/fm.raw";
            if (File.Exists(targetFileName))
            {
                File.Delete(targetFileName);
            }

            using (var fs = new FileStream(targetFileName, FileMode.CreateNew))
            {           
                foreach (var data in demodulatedData)
                {
                    var dataToWrite = BitConverter.GetBytes(data);
                    fs.Write(dataToWrite, 0, 2);
                }

                fs.Flush();
                fs.Close();
            }
        }
    }
}
