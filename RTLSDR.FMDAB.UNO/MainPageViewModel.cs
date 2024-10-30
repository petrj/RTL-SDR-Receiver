using RTLSDR.Common;
using RTLSDR.DAB;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RTLSDR.FMDAB.UNO;

public class MainPageViewModel  :  BaseViewModel
{
    private ObservableCollection<RadioService> _services { get; set; } = new ObservableCollection<RadioService>();

    public Dictionary<RadioService,DABService> DABServices { get; set; } = new Dictionary<RadioService, DABService>();

    public RadioService SelectedService { get; set; } = null;

    private readonly SynchronizationContext _syncContext;

    public MainPageViewModel()
    {
        _syncContext = SynchronizationContext.Current; // Capture UI thread context
    }

    private double _frequency  = 192352000;

    public string ActiveServiceName
    {
        get
        {
            if (SelectedService == null)
            {
                return "-";
            }

            return SelectedService.Name;
        }        
    }

    public void AddService(DABService service)
    {
        var radioService = new RadioService()        
        {
             Name = service.ServiceName             
        };

        DABServices.Add(radioService, service);

        Task.Run(() =>
        {
           _syncContext.Post(_ => _services.Add(radioService), null);
        });
    }

    public void SetActiveDABService(DABService dabService)
    {
        _syncContext.Post( delegate
        {
            foreach (var service in DABServices)
            {
                if (service.Value == dabService)
                {
                    SelectedService = service.Key;
                    OnPropertyChanged(nameof(ActiveServiceName));
                    OnPropertyChanged(nameof(SelectedService));

                    break;
                }
            }
        }, null);
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
            OnPropertyChanged(nameof(FreqUnitHR));            
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
