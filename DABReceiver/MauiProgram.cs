using CommunityToolkit.Maui;
using DABDriver;
using Microsoft.Extensions.Logging;

namespace DABReceiver
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

            return builder.Build();
        }
    }
}