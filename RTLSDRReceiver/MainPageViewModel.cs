using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using RTLSDR;
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
        private RTLSDR.RTLSDR _driver;

        public MainPageViewModel(ILoggingService loggingService, RTLSDR.RTLSDR driver)
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
