using LoggerService;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
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
        private object _lock = new object();

        public bool? Installed { get; set; } = null;

        private const int ReadBufferSize = 1000000; // 1 MB buffer

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

        public string RecordingDirectory { get; set; } = "/dev/null";

        public bool Recording { get; set; } = false;

        public DriverSettings Settings { get; private set; }

        public Queue<Command> _commandQueue;

        private int[] _supportedTcpCommands;

        private TunerTypeEnum _tunerType;
        private byte _gainCount;

        private string _magic;
        private string _deviceName;

        private int _sampleRate = 0;
        private int _frequency = 0;

        private BackgroundWorker _worker = null;
        private NetworkStream _stream;
        private ILoggingService _loggingService;

        private double _RTLBitrate = 0;
        private double _demodulationBitrate = 0;

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

            var buffer = new byte[ReadBufferSize];
            FileStream recordFileStream = null;

            var SDRBitRateCalculator = new BitRateCalculation(_loggingService,"SDR");
            var demodBitRateCalculator = new BitRateCalculation(_loggingService, "FMD");

            var UDPStreamer = new UDPStreamer(_loggingService, "127.0.0.1", Settings.Streamport);

            var demodulator = new FMDemodulator();

            while (!_worker.CancellationPending)
            {
                try
                {
                    if (State == DriverStateEnum.Connected)
                    {
                        var bytesRead = 0;
                        var bytesDemodulated = 0;

                        // reading data
                        if (_stream.CanRead)
                        {
                           bytesRead = _stream.Read(buffer, 0, buffer.Length);

                            //_loggingService.Debug($"RTLSDR: {bytesRead} bytes read");

                            if (bytesRead > 0)
                            {
                                if (Recording)
                                {
                                    if (recordFileStream == null)
                                    {
                                        if (!Directory.Exists(RecordingDirectory))
                                        {
                                            Recording = false;
                                        }
                                        else
                                        {
                                            var recordingFileName = Path.Combine(RecordingDirectory, $"RTL-SDR-QI-DATA-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.raw");

                                            if (System.IO.File.Exists(recordingFileName))
                                            {
                                                System.IO.File.Delete(recordingFileName);
                                            }

                                            recordFileStream = new FileStream(recordingFileName, FileMode.Create, FileAccess.Write);
                                        }
                                    }
                                    else
                                    {
                                        recordFileStream.Write(buffer, 0, bytesRead);
                                    }
                                }

                                var movedIQData = FMDemodulator.Move(buffer, bytesRead, -127);
                                var lowPassedData = demodulator.LowPass(movedIQData, 96000);
                                var demodulatedData = demodulator.FMDemodulate(lowPassedData);
                                var demodulatedBytes = FMDemodulator.ToByteArray(demodulatedData);

                                UDPStreamer.SendByteArray(demodulatedBytes, demodulatedData.Length);

                                bytesDemodulated += demodulatedData.Length;
                            }
                        }
                        else
                        {
                            // no data on input
                            Thread.Sleep(100);
                        }

                        // calculating speed

                        _RTLBitrate = SDRBitRateCalculator.GetBitRate(bytesRead);
                        _demodulationBitrate = demodBitRateCalculator.GetBitRate(bytesDemodulated);

                        // executing commands

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
                    else
                    {
                        _RTLBitrate = 0;
                        _demodulationBitrate = 0;

                        // no data on input
                        Thread.Sleep(200);
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.Error(ex);
                    State = DriverStateEnum.Error;
                }
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

        public TunerTypeEnum TunerType
        {
            get
            {
                return _tunerType;
            }
        }

        public long RTLBitrate
        {
            get
            {
                return Convert.ToInt32(_RTLBitrate);
            }
        }

        public long DemodulationBitrate
        {
            get
            {
                return Convert.ToInt32(_demodulationBitrate);
            }
        }

        public void Init(DriverInitializationResult driverInitializationResult)
        {
            _loggingService.Info("Initializing driver");

            _supportedTcpCommands = driverInitializationResult.SupportedTcpCommands;
            _deviceName = driverInitializationResult.DeviceName;
            RecordingDirectory = driverInitializationResult.OutputRecordingDirectory;

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

        public void Tune(int freq, int sampleRate, int correction = 0, bool autoGain = true, bool ifGain = false, bool autoAGC = true)
        {
            SetFrequency(freq);
            SetSampleRate(sampleRate);
            SetGainMode(!autoGain);

            if (TunerType == TunerTypeEnum.RTLSDR_TUNER_E4000)
            {
                SetIfGain(ifGain);
            }

            SetFrequencyCorrection(correction);
            SetAGCMode(autoAGC);
        }

        public void SetFrequency(int freq)
        {
            _loggingService.Info($"Setting frequency: {freq}");

            SendCommand(new Command(CommandsEnum.TCP_SET_FREQ, freq));

            _frequency = freq;
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

            _sampleRate = sampleRate;
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
