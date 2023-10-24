using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using Google.Android.Material.Snackbar;
using CommunityToolkit.Mvvm.Messaging;



namespace DABReceiver
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private DABDriver _driver;
        private static Android.Widget.Toast _instance;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _driver = new DABDriver();

            SubscribeMessages();

            base.OnCreate(savedInstanceState);
        }

        private void SubscribeMessages()
        {
            WeakReferenceMessenger.Default.Register<InitDriverMessage>(this, (s, m) =>
            {
                _driver.InitDriver(this);
            });
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            _driver.HandleInitResult(requestCode, resultCode, data);
        }
    }
}