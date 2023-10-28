using LoggerService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RTLSDR
{
    // https://hz.tools/rtl_tcp/
    public class RTLSDR
    {
        private Socket _socket;
        object _lock = new object();

        public bool? Installed { get; set; } = null;

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

        public DriverSettings Settings { get; private set; }

        public Queue<Command> _commandQueue;

        private int[] _supportedTcpCommands;

        private TunerTypeEnum _tunerType;
        private byte _gainCount;

        private string _magic;
        private string _deviceName;

        private BackgroundWorker _worker = null;
        private NetworkStream _stream;
        private ILoggingService _loggingService;

        public RTLSDR(ILoggingService loggingService)
        {
            Settings = new DriverSettings();
            _loggingService = loggingService;

            _commandQueue = new Queue<Command>();

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += _worker_DoWork;
            _worker.RunWorkerAsync();

            _loggingService.Info("Driver started");
        }

        public void SendCommand(Command command)
        {
            _loggingService.Info($"Enqueue command: {command}");

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
                try
                {
                    if (State == DriverStateEnum.Connected)
                    {
                        Command command = null;

                        lock (_lock)
                        {
                            if (_commandQueue.Count > 0)
                            {
                                command = _commandQueue.Dequeue();
                            }
                        }

                        if (command != null)
                        {
                            _loggingService.Info($"Sending command: {command}");

                            if (!_stream.CanWrite)
                            {
                                throw new Exception("Cannot write to stream");
                            }

                            _stream.Write(command.ToByteArray(), 0, 5);

                            _loggingService.Info($"Command {command} sent");
                        }
                    }
                } catch (Exception ex)
                {
                    _loggingService.Error(ex);
                    State = DriverStateEnum.Error;
                }

                Thread.Sleep(200);
            }

            _loggingService.Info($"Worker finished");
        }

        public string DeviceName
        {
            get
            {
                return $"{_deviceName} ({_magic})";
            }
        }

        public void Init(DriverInitializationResult driverInitializationResult)
        {
            _loggingService.Info("Initializing driver");

            _supportedTcpCommands = driverInitializationResult.SupportedTcpCommands;
            _deviceName = driverInitializationResult.DeviceName;

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
                _magic = Encoding.ASCII.GetString(buffer);

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

        public void Disconnect()
        {
            _loggingService.Info($"Disconnecting driver");

            SendCommand(new Command(CommandsEnum.TCP_ANDROID_EXIT, null));
            State = DriverStateEnum.NotInitialized;
        }

        public void SetFrequency(int freq)
        {
            _loggingService.Info($"Setting frequency: {freq}");

            SendCommand(new Command(CommandsEnum.TCP_SET_FREQ, freq));
        }

        public void SetFrequencyCorrection(int correction)
        {
            _loggingService.Info($"Setting frequency correction: {correction}");

            SendCommand(new Command(CommandsEnum.TCP_SET_FREQ_CORRECTION, correction));
        }

        public void SetSampleRate(int sampleRate)
        {
            _loggingService.Info($"Setting sample rate: {sampleRate}");

            SendCommand(new Command(CommandsEnum.TCP_SET_SAMPLE_RATE, sampleRate));
        }

        public void SetGainMode(bool manual)
        {
            _loggingService.Info($"Setting {(manual ? "manual" : "automatic")} gain mode");

            SendCommand(new Command(CommandsEnum.TCP_SET_GAIN_MODE, (int) (manual ? 1 : 0)));
        }

        public void SetGain(int gain)
        {
            _loggingService.Info($"Setting gain: {gain}");

            SendCommand(new Command(CommandsEnum.TCP_SET_GAIN, gain));
        }

        public void SetIfGain(bool ifGain)
        {
            _loggingService.Info($"Setting ifGain: {(ifGain ? "YES" : "NO")}");

            SendCommand(new Command(CommandsEnum.TCP_SET_IF_TUNER_GAIN, (short)0, (short)(ifGain ? 1 : 0)));
        }

        /// <summary>
        /// Automatic Gain Control
        /// </summary>
        /// <param name="m"></param>
        public void SetAGCMode(bool automatic)
        {
            _loggingService.Info($"Setting AGC: {(automatic ? "YES" : "NO")}");

            SendCommand(new Command(CommandsEnum.TCP_SET_AGC_MODE, (int)(automatic ? 1 : 0)));
        }
    }
}
