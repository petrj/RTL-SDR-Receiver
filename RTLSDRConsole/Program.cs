using System;
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

            var s = System.IO.Path.DirectorySeparatorChar;
            var IQData = File.ReadAllBytes(@"/temp/iq.data");
            var lowPassedData = FMDemodulator.LowPass(IQData, 48000);
            var demodulatedData = FMDemodulator.FMDemodulate(lowPassedData);

            /*
            using (var fs = new FileStream(@"c:\temp\demodulated.bin", FileMode.CreateNew))
            {

                foreach (var data in demodulatedData)
                {
                    var dataToWrite = BitConverter.GetBytes(data);
                    fs.Write(dataToWrite, 0, 2);
                }

                fs.Flush();
                fs.Close();

                //fs.Flush();
                //fs.Close();
            }
            */
        }
    }
}
