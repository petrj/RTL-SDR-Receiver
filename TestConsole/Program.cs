// See https://aka.ms/new-console-template for more information

using RTLSDR;

var IQData = File.ReadAllBytes(@"..\..\..\..\Tests\TestData\QI-DATA");
var lowPassedData = FMDemodulator.LowPass(IQData, 48000);
var demodulatedData = FMDemodulator.FMDemodulate(lowPassedData);

File.WriteAllBytes(@"c:\temp\demodulated.bin", demodulatedData);

Console.WriteLine("Hello, World!");
