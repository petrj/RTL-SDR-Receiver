using System.ComponentModel;

namespace RTLSDR.FMDAB.UNO;

public class MainPageViewModel  :  BaseViewModel
{
    public MainPageViewModel()
    {
 
    }

    private double _frequency  = 192352000;

    public double Frequency  
    {
        get
        {
            return _frequency;
        }
        set
        {
            _frequency = value;
            OnPropertyChanged(nameof(Frequency));
            OnPropertyChanged(nameof(FreqHR));
        }
    }

    public string FreqHR
    {
        get
        {
            if (Frequency > 1E+6)
            {
                return (Frequency / 1000000).ToString("N2") + " MHz";    
            }
            if (Frequency > 1E+3)
            {
                return (Frequency / 1000).ToString("N2") + " kHz";    
            }

            return (Frequency).ToString("N0") + " Hz";                
        }
    }
}
