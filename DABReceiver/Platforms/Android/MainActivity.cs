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
        private const int StartRequestCode = 1000;
        private static Android.Widget.Toast _instance;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            SubscribeMessages();

            base.OnCreate(savedInstanceState);
        }

        private void SubscribeMessages()
        {
            WeakReferenceMessenger.Default.Register<InitDriverMessage>(this, (s, m) =>
            {
                InitDriver();
            });
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
                ShowToastMessage("Driver not installed");
            }
            catch (Exception ex)
            {
                ShowToastMessage("Driver initializing failed");
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == StartRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    ShowToastMessage("Driver successfully initialized");
                } else
                {
                    ShowToastMessage("Driver initialization failed");
                }
            }
        }

        private void ShowToastMessage(string message, int AppFontSize = 0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    _instance?.Cancel();
                    _instance = Android.Widget.Toast.MakeText(Android.App.Application.Context, message, ToastLength.Short);

                    TextView textView;
                    Snackbar snackBar = null;

                    var tView = _instance.View;
                    if (tView == null)
                    {
                        // Since Android 11, custom toast is deprecated - using snackbar instead:

                        //Activity activity = CrossCurrentActivity.Current.Activity;
                        var view = FindViewById(Android.Resource.Id.Content);

                        snackBar = Snackbar.Make(view, message, Snackbar.LengthLong);

                        textView = snackBar.View.FindViewById<TextView>(Resource.Id.snackbar_text);
                    }
                    else
                    {
                        // using Toast

                        tView.Background.SetColorFilter(Android.Graphics.Color.Gray, PorterDuff.Mode.SrcIn); //Gets the actual oval background of the Toast then sets the color filter
                        textView = (TextView)tView.FindViewById(Android.Resource.Id.Message);
                        textView.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
                    }

                    var minTextSize = textView.TextSize; // 16

                    textView.SetTextColor(Android.Graphics.Color.White);

                    var screenHeightRate = 0;

                    //configuration font size:

                    //Normal = 0,
                    //AboveNormal = 1,
                    //Big = 2,
                    //Biger = 3,
                    //VeryBig = 4,
                    //Huge = 5

                    if (DeviceDisplay.MainDisplayInfo.

                    Height < DeviceDisplay.MainDisplayInfo.Width)
                    {
                        // Landscape

                        screenHeightRate = Convert.ToInt32(DeviceDisplay.MainDisplayInfo.Height / 16.0);
                        textView.SetMaxLines(5);
                    }
                    else
                    {
                        // Portrait

                        screenHeightRate = Convert.ToInt32(DeviceDisplay.MainDisplayInfo.Height / 32.0);
                        textView.SetMaxLines(5);
                    }

                    var fontSizeRange = screenHeightRate - minTextSize;
                    var fontSizePerValue = fontSizeRange / 5;

                    var fontSize = minTextSize + AppFontSize * fontSizePerValue;

                    textView.SetTextSize(Android.Util.ComplexUnitType.Px, Convert.ToSingle(fontSize));

                    if (snackBar != null)
                    {
                        snackBar.Show();
                    }
                    else
                    {
                        _instance.Show();
                    }
                }
                catch (Exception ex)
                {

                }
            });
        }
    }
}