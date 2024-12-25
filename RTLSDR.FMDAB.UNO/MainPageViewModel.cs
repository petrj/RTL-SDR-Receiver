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

    private DABProcessorState _state;

    public MainPageViewModel()
    {
        _syncContext = SynchronizationContext.Current; // Capture UI thread context
    }

    private int _frequency  = 192352000;

    public void SetNextFrequency()
    {
        double _newFreq = -1;
        foreach (var freq in DABConstants.DABFrequenciesMHz)
        {
            _newFreq = freq.Key;
 
            if (freq.Key>Frequency/1E+6)
            {
                break;
            }
        }

        Frequency = Convert.ToInt32(_newFreq*1E+6);
    }

    public void SetPreviousFrequency()
    {
        double _newFreq = -1;
        foreach (var freq in DABConstants.DABFrequenciesMHz.Reverse())
        {
            _newFreq = freq.Key;
 
            if (freq.Key<Frequency/1E+6)
            {
                break;
            }
        }

        Frequency = Convert.ToInt32(_newFreq*1E+6);
    }    

    public void UpdateGUI()
    {
        _syncContext.Post(delegate
        {
            OnPropertyChanged(nameof(SelectedService));
            OnPropertyChanged(nameof(ActiveServiceName));
            OnPropertyChanged(nameof(Frequency));
            OnPropertyChanged(nameof(FreqHR));
            OnPropertyChanged(nameof(FreqUnitHR));

            OnPropertyChanged(nameof(State));

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

    public void ClearServicies()
    {
        Task.Run(() =>
        {
           _syncContext.Post(_ => _services.Clear(), null);
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

    public DABProcessorState State
    {
        get
        {
            return _state;
        }
        set
        {
            _state = value;
            UpdateGUI();
        }
    }

    public int Frequency
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
                return (Convert.ToDouble(Frequency) / 1000000).ToString("N2");
            }
            if (Frequency > 1E+3)
            {
                return (Convert.ToDouble(Frequency) / 1000).ToString("N2");
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
