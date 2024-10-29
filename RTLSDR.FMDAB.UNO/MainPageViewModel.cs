using RTLSDR.Common;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RTLSDR.FMDAB.UNO;

public class MainPageViewModel  :  BaseViewModel
{
    private ObservableCollection<RadioService> _services { get; set; } = new ObservableCollection<RadioService>();
    public RadioService SelectedService { get; set; } = null;

    private readonly SynchronizationContext _syncContext;

    public MainPageViewModel()
    {
        _syncContext = SynchronizationContext.Current; // Capture UI thread context
    }

    private double _frequency  = 192352000;

    public void AddService(RadioService service)
    {
        Task.Run(() =>
        {
           _syncContext.Post(_ => _services.Add(service), null);
        });
    }

    public ObservableCollection<RadioService> Services
    {
        get
        {
            return _services;
        }
    }

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
