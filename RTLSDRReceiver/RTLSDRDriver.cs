﻿using CommunityToolkit.Mvvm.Messaging;
using LoggerService;
using NLog.Fluent;
using RTLSDRReceiver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RTLSDRReceiver
{
    // https://hz.tools/rtl_tcp/
    public class RTLSDRDriver
    {
        private Socket _socket;
        object _lock = new object();

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

        public RTLSDRDriverSettings Settings { get; private set; }

        public Queue<RTLSDRCommand> _commandQueue;

        private int[] _supportedTcpCommands;

        private TunerTypeEnum _tunerType;
        private byte _gainCount;

        private BackgroundWorker _worker = null;
        private NetworkStream _stream;
        private ILoggingService _loggingService;

        public RTLSDRDriver(ILoggingService loggingService)
        {
            Settings = new RTLSDRDriverSettings();
            _loggingService = loggingService;

            _commandQueue = new Queue<RTLSDRCommand>();

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork;
            _worker.RunWorkerAsync();

            _loggingService.Info("Driver started");
        }

        public void SendCommand(RTLSDRCommand command)
        {
            lock (_lock)
            {
                _commandQueue.Enqueue(command);
            }
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info($"Worker started");

            while (!_worker.CancellationPending)
            {
                if (State == DriverStateEnum.Connected)
                {
                    RTLSDRCommand command = null;

                    lock (_lock)
                    {
                        if (_commandQueue.Count > 0)
                        {
                            command = _commandQueue.Dequeue();
                        }
                    }

                    if (command != null)
                    {
                        _loggingService.Info($"Sending command: {command.Command}");

                        _stream.Write(command.ToByteArray(), 0, 5);

                        _loggingService.Info($"Command {command.Command} sent");
                    }
                }

                Thread.Sleep(200);
            }

            _loggingService.Info($"Worker finished");
        }

        public void Init(RTLSDRDriverInitializationResult driverInitializationResult)
        {
            _loggingService.Info("Initializing driver");

            _supportedTcpCommands = driverInitializationResult.SupportedTcpCommands;
            State = DriverStateEnum.Initialized;

            Connect();
        }

        private void Connect()
        {
            try
            {
                _loggingService.Info($"Connecting driver on {Settings.IP}:{Settings.Port}");

                var ipAddress = IPAddress.Parse(Settings.IP);
                var endPoint = new IPEndPoint(ipAddress, Settings.Port);

                _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                // Set socket options:
                _socket.NoDelay = true;
                _socket.ReceiveTimeout = 1000;
                _socket.SendTimeout = 1000;

                _socket.Connect(endPoint);

                _stream = new NetworkStream(_socket, FileAccess.ReadWrite, false);

                // Read magic value:
                byte[] buffer = new byte[4];
                if (_stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    _loggingService.Error(null, "Could not read magic value");
                    return;
                }
                //var magic = new String(buffer, "ASCII");

                // Read tuner type:
                if (_stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    _loggingService.Error(null, "Could not read tuner type");
                    return;
                }

                try
                {
                    _tunerType = (TunerTypeEnum)buffer[3];
                }
                catch
                {
                    _loggingService.Info("Unknown tuner type");
                }

                // Read gain count
                if (_stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                {
                    _loggingService.Error(null, "Could not read gain count");
                    return ;
                }

                _gainCount = buffer[3];

                _loggingService.Info($"Driver connected");

                State = DriverStateEnum.Connected;

            } catch (Exception ex)
            {
                _loggingService.Error(ex);
                State = DriverStateEnum.Error;
            }
        }
    }
}
