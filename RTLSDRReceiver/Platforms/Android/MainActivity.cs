using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using Google.Android.Material.Snackbar;
using CommunityToolkit.Mvvm.Messaging;

namespace RTLSDRReceiver
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int StartRequestCode = 1000;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            SubscribeMessages();

            base.OnCreate(savedInstanceState);
        }

        private void SubscribeMessages()
        {
            WeakReferenceMessenger.Default.Register<InitDriverMessage>(this, (sender, obj) =>
            {
                if (obj.Value is RTLSDRDriverSettings settings)
                {
                    InitDriver(settings.Port, settings.SampleRate);
                }
            });
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == StartRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    WeakReferenceMessenger.Default.Send(new DriverInitializedMessage(new RTLSDRDriverInitializationResult()
                    {
                        SupportedTcpCommands = data.GetIntArrayExtra("supportedTcpCommands"),
                        DeviceName = data.GetStringExtra("deviceName")
                    }));
                    WeakReferenceMessenger.Default.Send(new ToastMessage("Driver successfully initialized"));
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new ToastMessage("Driver initialization failed"));
                }
            }
        }

        private void InitDriver(int port = 1234, int samplerate = 2048000)
        {
            try
            {
                var req = new Intent(Intent.ActionView);
                req.SetData(Android.Net.Uri.Parse($"iqsrc://-a 127.0.0.1 -p \"{port}\" -s \"{samplerate}\""));
                req.PutExtra(Intent.ExtraReturnResult, true);

                StartActivityForResult(req, StartRequestCode);
            }
            catch (ActivityNotFoundException ex)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage("Driver not installed"));
            }
            catch (Exception ex)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage("Driver initializing failed"));
            }
        }
    }
}