using CommunityToolkit.Mvvm.Messaging;

namespace DABReceiver
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            WeakReferenceMessenger.Default.Send(new InitDriverMessage());
        }

        private void Btn_Clicked(object sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new InitDriverMessage());
        }
    }
}