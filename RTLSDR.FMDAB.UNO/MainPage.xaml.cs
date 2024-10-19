namespace RTLSDR.FMDAB.UNO;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; set; }

    public MainPage()
    {
        this.InitializeComponent();

        this.DataContext = ViewModel = new MainPageViewModel();
    }

    private void OnTuneButtonClicked(object sender, EventArgs e)
    {        
        ViewModel.Frequency = 104000000;
    } 
}    

