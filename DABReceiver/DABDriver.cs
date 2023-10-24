using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DABReceiver
{
    public class DABDriver
    {
        private const int StartRequestCode = 1000;

        public bool Initialized { get; private set; } = false;

        public DABDriverSettings Settings { get; private set; } = new DABDriverSettings();

        private int[] _supportedTcpCommands;

        public void Init(DABDriverInitializationResult driverInitializationResult)
        {
            _supportedTcpCommands = driverInitializationResult.SupportedTcpCommands;
            Initialized = true;
        }
    }
}
