using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace RTLSDRReceiver
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                //.UseCommunityToolkitHelper()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
		builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<MainPage>();

            builder.Services.AddSingleton<LoggerProvider>();
            builder.Services.AddSingleton<ILoggingProvider, LoggerProvider>();
            builder.Services.AddSingleton<IAppSettings, AppSettings>();

            return builder.Build();
        }
    }
}