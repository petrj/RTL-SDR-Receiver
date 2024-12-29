using LoggerService;
using RTLSDR.Audio;
using RTLSDR.DAB;
using RTLSDR.FMDAB.Console;
using Uno.UI.Runtime.Skia;

namespace RTLSDR.FMDAB.UNO;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        UNOAppParams.ParseArgs(args);

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
