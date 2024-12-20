using LoggerService;
using RTLSDR.Audio;
using Uno.UI.Runtime.Skia;

namespace RTLSDR.FMDAB.UNO;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        var host = SkiaHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWindows()
            .Build();

        host.Run();
    }
}
