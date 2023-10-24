using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.Messaging;
using DABDriver;
using LoggerService;

namespace DABReceiver
{
    public partial class MainPage : ContentPage
    {
        private ILoggingService _loggingService;
        private DABDriver _driver;

        public MainPage(ILoggingProvider loggingProvider)
        {
            InitializeComponent();

            _loggingService = loggingProvider.GetLoggingService();
            _driver = new DABDriver();

            _loggingService.Info("App started");

            WeakReferenceMessenger.Default.Register<ToastMessage>(this, (r, m) =>
            {
                Task.Run(async () =>
                {
                    await ShowToastMessage(m.Value);
                });
            });

            WeakReferenceMessenger.Default.Register<DriverInitializedMessage>(this, (sender, obj) =>
            {
                if (obj.Value is DABDriverInitializationResult settings)
                {
                    _driver.Init(settings);
                }
            });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
        }

        private async Task ShowToastMessage(string message, int seconds = 3, int AppFontSize = 0)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Colors.Gray,
                TextColor = Colors.Black,
                //ActionButtonTextColor = Colors.Blue,
                CornerRadius = new CornerRadius(10),
                Font = Microsoft.Maui.Font.SystemFontOfSize(14, FontWeight.Bold),
                ActionButtonFont = Microsoft.Maui.Font.SystemFontOfSize(14)
                //CharacterSpacing = 0.5
            };

            //Action action = async () => await DisplayAlert("Snackbar ActionButton Tapped", "The user has tapped the Snackbar ActionButton", "OK");

            var snackbar = Snackbar.Make(message, null, "", TimeSpan.FromSeconds(seconds), snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        private async void Btn_Clicked(object sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new InitDriverMessage(_driver.Settings));
        }
    }
}