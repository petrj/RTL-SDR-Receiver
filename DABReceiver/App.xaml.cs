namespace DABReceiver
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            UserAppTheme = AppTheme.Dark;

            MainPage = new AppShell();
        }
    }
}