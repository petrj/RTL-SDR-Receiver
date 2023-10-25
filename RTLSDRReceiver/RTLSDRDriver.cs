using CommunityToolkit.Mvvm.Messaging;
using RTLSDRReceiver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class RTLSDRDriver
    {
        public bool Initialized { get; private set; } = false;

        public RTLSDRDriverSettings Settings { get; private set; } = new RTLSDRDriverSettings();

        private int[] _supportedTcpCommands;

        public void Init(RTLSDRDriverInitializationResult driverInitializationResult)
        {
            _supportedTcpCommands = driverInitializationResult.SupportedTcpCommands;
            Initialized = true;
        }
    }
}
