// See https://aka.ms/new-console-template for more information

using LoggerService;
using RTLSDR;
using System.IO;

var logger = new BasicLoggingService();
logger.Info("RTL SDR Test Console");


///var IQData = File.ReadAllBytes(@"..\..\..\..\Tests\TestData\QI-DATA");
var IQData = File.ReadAllBytes(@"C:\temp\RTL-SDR-QI-DATA-2023-10-29-23-04-41.raw");
var lowPassedData = FMDemodulator.LowPass(IQData, 48000);
var demodulatedData = FMDemodulator.FMDemodulate(lowPassedData);


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


//File.WriteAllBytes(@"c:\temp\demodulated.bin", demodulatedData);
