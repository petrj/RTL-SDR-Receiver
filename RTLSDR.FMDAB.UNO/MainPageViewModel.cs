using RTLSDR.Common;
using RTLSDR.DAB;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RTLSDR.FMDAB.UNO;

public class MainPageViewModel  :  BaseViewModel
{
    private ObservableCollection<IAudioService> _services { get; set; } = new ObservableCollection<IAudioService>();

    private readonly SynchronizationContext _syncContext;
    private IAudioService _selectedService = null;

    public MainPageViewModel()
    {
        _syncContext = SynchronizationContext.Current; // Capture UI thread context
    }

    private double _frequency  = 192352000;

    public void UpdateGUI()
    {
        _syncContext.Post(delegate
        {
            OnPropertyChanged(nameof(SelectedService));
            OnPropertyChanged(nameof(ActiveServiceName));
            OnPropertyChanged(nameof(Frequency));
            OnPropertyChanged(nameof(FreqHR));
            OnPropertyChanged(nameof(FreqUnitHR));
        }, null);
    }

    public IAudioService SelectedService
    {
        get
        {
            return _selectedService;
        }
        set
        {
            _selectedService = value;
            UpdateGUI();
        }
    }

    public string ActiveServiceName
    {
        get
        {
            if (SelectedService == null)
            {
                return "-";
            }

            if (SelectedService is IAudioService audioService)
            {
                return audioService.ServiceName;
            }

            return "-";
        }
    }

    public void AddService(IAudioService service)
    {
        Task.Run(() =>
        {
           _syncContext.Post(_ => _services.Add(service), null);
        });
    }

    public void SetActiveDABService(IAudioService dabService)
    {
        SelectedService = dabService;
        UpdateGUI();
    }

    public ObservableCollection<IAudioService> Services
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
            UpdateGUI();
        }
    }

    public string FreqHR
    {
        get
        {
            if (Frequency > 1E+6)
            {
                return (Frequency / 1000000).ToString("N2");
            }
            if (Frequency > 1E+3)
            {
                return (Frequency / 1000).ToString("N2");
            }

            return (Frequency).ToString("N0");
        }
    }

    public string FreqUnitHR
    {
        get
        {
            if (Frequency > 1E+6)
            {
                return "MHz";
            }
            if (Frequency > 1E+3)
            {
                return "kHz";
            }
            return "Hz";
        }
    }
}
