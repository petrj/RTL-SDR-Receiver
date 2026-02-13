using LoggerService;
using RTLSDR.Audio;
using RTLSDR.Common;
using System.Runtime.InteropServices;

IRawAudioPlayer rawAudioPlayer;
ILoggingService loggingService = new BasicLoggingService();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    rawAudioPlayer = new NAudioRawAudioPlayer(loggingService);       // Windows only
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    //rawAudioPlayer = new AlsaSoundAudioPlayer();                     // Linux only
    rawAudioPlayer = new VLCSoundAudioPlayer();                     // Linux + Windows
}
else
{
    // unsupported platform
    rawAudioPlayer = new NoAudioRawAudioPlayer();                    // dummy interface
}

rawAudioPlayer.Init(new AudioDataDescription()
{
 BitsPerSample = 16,
  Channels = 2,
   SampleRate = 44100
}, loggingService);


bool quit = false;

Task.Run(() =>
{
    try
    {
        using (var fs = new FileStream("samples" + System.IO.Path.DirectorySeparatorChar + "16bLE44st.wav", FileMode.Open, FileAccess.Read))
        {
            byte[] buffer = new byte[8000];
            int bytesRead;
            while (!quit && ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0))
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


rawAudioPlayer.Play();

Console.WriteLine("Press Enter...");
Console.ReadLine();

rawAudioPlayer.Stop();
quit = true;
