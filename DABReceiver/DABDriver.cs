using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DABReceiver
{
    public class DABDriver
    {
        private const int StartRequestCode = 1000;

        public bool Initialized { get; private set; } = false;

        public int Port { get; private set; } = 1234;
        public int SampleRate { get; private set; } = 2048000;

        public void InitDriver(MauiAppCompatActivity activity, int port = 1234, int samplerate = 2048000)
        {
            try
            {
                Port = port;
                SampleRate = samplerate;

                var req = new Intent(Intent.ActionView);
                req.SetData(Android.Net.Uri.Parse($"iqsrc://-a 127.0.0.1 -p \"{Port}\" -s \"{SampleRate}\""));
                req.PutExtra(Intent.ExtraReturnResult, true);

                activity.StartActivityForResult(req, StartRequestCode);
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

        public void HandleInitResult(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode == StartRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    WeakReferenceMessenger.Default.Send(new ToastMessage("Driver successfully initialized"));

                    var supportedTcpCommands = data.GetIntArrayExtra("supportedTcpCommands");
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new ToastMessage("Driver initialization failed"));
                }
            }
        }
    }
}
