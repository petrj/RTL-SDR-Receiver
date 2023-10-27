using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class MainPageViewModel : BaseNotifableObject
    {
        private ILoggingService _loggingService;
        private RTLSDRDriver _driver;

        public MainPageViewModel(ILoggingService loggingService, RTLSDRDriver driver)
        {
            _driver = driver;
            _loggingService = loggingService;

            WeakReferenceMessenger.Default.Register<NotifyDriverIconChangeMessage>(this, (recipient, msg) =>
            {
                OnPropertyChanged(nameof(DriverIcon));
            });
        }

        public string DriverIcon
        {
            get
            {
                if (_driver == null || _driver.State != DriverStateEnum.Connected)
                {
                    return "disconnected";
                }

                return "connected";
            }
        }
    }
}
