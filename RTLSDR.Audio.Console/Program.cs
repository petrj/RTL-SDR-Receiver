using LoggerService;
using RTLSDR.Audio;
using RTLSDR.Common;
using System.Runtime.InteropServices;
using System.Diagnostics;


CancellationTokenSource cancellationToken = new CancellationTokenSource();

IRawAudioPlayer rawAudioPlayer;
ILoggingService loggingService = new BasicLoggingService();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    rawAudioPlayer = new NAudioRawAudioPlayer(loggingService);       // Windows only
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    rawAudioPlayer = new AlsaSoundAudioPlayer();                     // Linux only
}
else
{
    // unsupported platform
    rawAudioPlayer = new NoAudioRawAudioPlayer();                    // dummy interface
}

//rawAudioPlayer = new VLCSoundAudioPlayer();                     // Linux + Windows


void ReadFile(string fName, CancellationTokenSource cancellationToken)
{
    Task.Run(() =>
    {
        try
        {
            using (var fs = new FileStream(fName, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[8000];
                int bytesRead;
                while (!cancellationToken.IsCancellationRequested && ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0))
                {
                    if (bytesRead < buffer.Length)
                    {
                        // If we read less than the buffer size, we need to trim the buffer
                        byte[] trimmedBuffer = new byte[bytesRead];
                        Array.Copy(buffer, trimmedBuffer, bytesRead);
                        rawAudioPlayer.AddPCM(trimmedBuffer);
                    }
                    else
                    {
                        rawAudioPlayer.AddPCM(buffer);
                    }

                    Thread.Sleep(50);
                }
            }

            Console.WriteLine($"File processed");

        } catch (Exception ex)
        {
            Console.WriteLine($"Error reading audio file: {ex.Message}");
        }
    });

}

var folder = AppContext.BaseDirectory;
var fName = Path.Join(folder,"samples","16bLE44st.wav");

var desc44 = new AudioDataDescription()
{
 BitsPerSample = 16,
  Channels = 2,
   SampleRate = 44100
};


rawAudioPlayer.Init(desc44, loggingService);
rawAudioPlayer.Play();
ReadFile(fName,cancellationToken);

Console.WriteLine("Press Enter...");
Console.ReadLine();

rawAudioPlayer.Stop();
cancellationToken.Cancel();

// reinit test

rawAudioPlayer.Init(desc44, loggingService);
rawAudioPlayer.Play();
ReadFile(fName,cancellationToken);

Console.WriteLine("Press Enter...");
Console.ReadLine();

rawAudioPlayer.Stop();
cancellationToken.Cancel();