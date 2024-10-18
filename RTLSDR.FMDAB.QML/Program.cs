using Qml.Net.Runtimes;
using Qml.Net;

RuntimeManager.DiscoverOrDownloadSuitableQtRuntime();

QQuickStyle.SetStyle("Material");

using (var application = new QGuiApplication(args))
{
    using (var qmlEngine = new QQmlApplicationEngine())
    {
        qmlEngine.Load("Main.qml");

        return application.Exec();
    }
}