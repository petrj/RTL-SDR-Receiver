using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using RTLSDRReceiver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class RTLSDRDriver
    {
        private Socket _socket;

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

        public RTLSDRDriverSettings Settings { get; private set; }

        private int[] _supportedTcpCommands;
        private BackgroundWorker _worker = null;
        private ILoggingService _loggingService;

        public RTLSDRDriver(ILoggingService loggingService)
        {
            Settings = new RTLSDRDriverSettings();
            _loggingService = loggingService;

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork;

            _loggingService.Info("Driver started");
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info($"Worker started");

            while (!_worker.CancellationPending)
            {
                if (State == DriverStateEnum.Connected)
                {

                }

                Thread.Sleep(1000);
            }

            _loggingService.Info($"Worker finished");
        }

        public void Init(RTLSDRDriverInitializationResult driverInitializationResult)
        {
            _loggingService.Info("Initializing driver");

            _supportedTcpCommands = driverInitializationResult.SupportedTcpCommands;
            State = DriverStateEnum.Initialized;

            Connect();
            _worker.RunWorkerAsync();
        }

        private void Connect()
        {
            try
            {
                _loggingService.Info($"Connecting driver on {Settings.IP}:{Settings.Port}");

                var ipAddress = IPAddress.Parse(Settings.IP);
                var endPoint = new IPEndPoint(ipAddress, Settings.Port);

                _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                _socket.Bind(endPoint);
                _socket.Listen(10);

                State = DriverStateEnum.Connected;

            } catch (Exception ex)
            {
                State = DriverStateEnum.Error;
            }
        }
    }
}
