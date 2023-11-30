using LoggerService;
using NLog;
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
        private object _dataLock = new object();

        public bool? Installed { get; set; } = null;

        private const int ReadBufferSize = 1000000; // 1 MB buffer

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

        public string RecordingDirectory { get; set; } = "/dev/null";

        public bool Recording { get; set; } = false;
        public bool DeEmphasis { get; set; } = false;
        public bool FastAtan { get; set; } = true;

        public DriverSettings Settings { get; private set; }

        FMDemodulator _demodulator = new FMDemodulator();
        PowerCalculation _powerCalculator = new PowerCalculation();
        BitRateCalculation _demodBitRateCalculator;

        public Queue<Command> _commandQueue;

        private int[] _supportedTcpCommands;

        private TunerTypeEnum _tunerType;
        private byte _gainCount;

        private string _magic;
        private string _deviceName;

        private int _frequency = 104000000;

        private BackgroundWorker _dataWorker = null;
        private BackgroundWorker _commandWorker = null;

        private NetworkStream _stream;
        private ILoggingService _loggingService;

        private double _RTLBitrate = 0;
        private double _demodulationBitrate = 0;
        private double _powerPercent = 0;
        private double _power = 0;

        public const int FMMinFrequenctKHz = 88000;
        public const int FMMaxFrequenctKHz = 108000;

        private short[] _demodBuffer = new short[20000000];

        public RTLSDR(ILoggingService loggingService)
        {
            Settings = new DriverSettings();
            _loggingService = loggingService;

            _commandQueue = new Queue<Command>();

            _demodBitRateCalculator = new BitRateCalculation(_loggingService, "FMD");

            _dataWorker = new BackgroundWorker();
            _dataWorker.WorkerSupportsCancellation = true;
            _dataWorker.DoWork += _dataWorker_DoWork;
            _dataWorker.RunWorkerAsync();

            _commandWorker = new BackgroundWorker();
            _commandWorker.WorkerSupportsCancellation = true;
            _commandWorker.DoWork += _commandWorker_DoWork;
            _commandWorker.RunWorkerAsync();

            _loggingService.Info("Driver started");
        }

        private void _commandWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info($"_commandWorker started");

            while (!_commandWorker.CancellationPending)
            {
                try
                {
                    if (State == DriverStateEnum.Connected)
                    {
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
                        // no data on input
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.Error(ex);
                    State = DriverStateEnum.Error;
                }
            }

            _loggingService.Info($"_commandWorker finished");
        }

        public string DemodMonoStat(byte[] IQData, bool fastatan, int port = 4555)
        {
            var demodulator = new FMDemodulator();
            var UDPStreamer = new UDPStreamer(_loggingService, "127.0.0.1", port);
            var demodBuffer = new short[60000000];

            var timeBeforeLowPass = DateTime.Now;

            var lowPassedDataLength = demodulator.LowPassWithMoveParallel(IQData, demodBuffer, IQData.Length, 96000, -127);

            var timeBeforeDemodulate = DateTime.Now;

            var demodulatedDataMonoLength = demodulator.FMDemodulate(demodBuffer, lowPassedDataLength, fastatan);

            var timeAfterDemodulate = DateTime.Now;

            var finalBytes = FMDemodulator.ToByteArray(demodBuffer, demodulatedDataMonoLength);
            var timeAfterByteArray = DateTime.Now;

            UDPStreamer.SendByteArray(finalBytes, finalBytes.Length);

            var timeAfterSend = DateTime.Now;

            var bytesPerOneSec = 96000;
            var recordTime = demodulatedDataMonoLength / bytesPerOneSec;

            var res = new StringBuilder();

            res.AppendLine($"Bytes total         : {IQData.Length} bytes");
            res.AppendLine($"-------------------------------");
            res.AppendLine($"LowPass             : {(timeBeforeLowPass - timeBeforeDemodulate).TotalMilliseconds.ToString("N2")} ms");
            res.AppendLine($"Demodulation        : {(timeAfterDemodulate - timeBeforeDemodulate).TotalMilliseconds.ToString("N2")} ms");
            res.AppendLine($"->byte[]            : {(timeAfterByteArray - timeAfterDemodulate).TotalMilliseconds.ToString("N2")} ms");
            res.AppendLine($"->UDP               : {(timeAfterSend - timeAfterByteArray).TotalMilliseconds.ToString("N2")} ms");
            res.AppendLine($"Overall             : {(timeAfterSend - timeBeforeLowPass).TotalMilliseconds.ToString("N2")} ms");
            res.AppendLine($"-------------------------------");
            res.AppendLine($"Record time         : {recordTime} sec");

            return res.ToString();
        }

        private byte[] Demodulate(byte[] audioBufferBytes, int audioBufferBytesCount)
        {
            var demodTimeStart = DateTime.Now;
            int finalCount;
            var timeBeforeLowPass = DateTime.Now;
            var timeBeforeDemodulate = DateTime.Now;

            if (DeEmphasis)
            {
                var demodBufferLength = _demodulator.LowPassWithMove(audioBufferBytes, _demodBuffer, audioBufferBytesCount, 170000, -127);

                _powerPercent = _powerCalculator.GetPowerPercent(_demodBuffer, demodBufferLength);
                _power = PowerCalculation.GetCurrentPower(_demodBuffer[0], _demodBuffer[1]);

                var demodulatedDataLength = _demodulator.FMDemodulate(_demodBuffer, demodBufferLength, FastAtan);

                _demodulator.DeemphFilter(_demodBuffer, demodulatedDataLength, 170000);
                finalCount = _demodulator.LowPassReal(_demodBuffer, demodulatedDataLength, 170000, Settings.FMSampleRate);
            }
            else
            {
                var demodBufferLength = _demodulator.LowPassWithMove(audioBufferBytes, _demodBuffer, audioBufferBytesCount, Settings.FMSampleRate, -127);

                _powerPercent = _powerCalculator.GetPowerPercent(_demodBuffer, demodBufferLength);
                _power = PowerCalculation.GetCurrentPower(_demodBuffer[0], _demodBuffer[1]);

                timeBeforeDemodulate = DateTime.Now;
                finalCount = _demodulator.FMDemodulate(_demodBuffer, demodBufferLength, FastAtan);
            }

            var timeAfterDemodulate = DateTime.Now;
            var finalBytes = FMDemodulator.ToByteArray(_demodBuffer, finalCount);
            var timeAfterByteArray = DateTime.Now;

            _demodulationBitrate = _demodBitRateCalculator.GetBitRate(finalBytes.Length);

            var timeAfterSend = DateTime.Now;

            // computing demodulation time:
            var audioBufferSecsTime = audioBufferBytesCount / 2.00 / (double)Settings.FMSampleRate;
            var demodTimeSecs = (DateTime.Now - demodTimeStart).TotalSeconds;
            if (demodTimeSecs > audioBufferSecsTime)
            {
                _loggingService.Info($">>>>>>>>>>>>>>> Error - demodulation is too slow: demod time secs: {demodTimeSecs.ToString("N2")}, buffer time: {audioBufferSecsTime.ToString("N2")} s ({audioBufferBytesCount.ToString("N0")} bytes)");
                _loggingService.Info($"LowPass             : {(timeBeforeLowPass - timeBeforeDemodulate).TotalMilliseconds.ToString("N2")} ms");
                _loggingService.Info($"Demodulation        : {(timeAfterDemodulate - timeBeforeDemodulate).TotalMilliseconds.ToString("N2")} ms");
                _loggingService.Info($"->byte[]            : {(timeAfterByteArray - timeAfterDemodulate).TotalMilliseconds.ToString("N2")} ms");
                _loggingService.Info($"->UDP               : {(timeAfterSend - timeAfterByteArray).TotalMilliseconds.ToString("N2")} ms");
                _loggingService.Info($"Overall             : {(timeAfterSend - timeBeforeLowPass).TotalMilliseconds.ToString("N2")} ms");
                _loggingService.Info($"-----------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            }

            return finalBytes;
        }

        private void Record(FileStream recordFileStream, byte[] buffer, int bytesRead)
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
            else
            {
                // not recording
                if (recordFileStream != null)
                {
                    recordFileStream.Close();
                    recordFileStream = null;
                }
            }
        }

        private void _dataWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _loggingService.Info($"_dataWorker started");

            var buffer = new byte[ReadBufferSize];
            FileStream recordFileStream = null;

            var SDRBitRateCalculator = new BitRateCalculation(_loggingService,"SDR");
            var UDPStreamer = new UDPStreamer(_loggingService, "127.0.0.1", Settings.Streamport);

            while (!_dataWorker.CancellationPending)
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
                                Record(recordFileStream, buffer, bytesRead);

                                var finalBytes = Demodulate(buffer, bytesRead);

                                UDPStreamer.SendByteArray(finalBytes, finalBytes.Length);
                            } else
                            {
                                _powerPercent = 0;
                            }
                        }
                        else
                        {
                            // no data on input
                            _powerPercent = 0;
                            Thread.Sleep(10);
                        }

                        // calculating speed

                        _RTLBitrate = SDRBitRateCalculator.GetBitRate(bytesRead);
                    }
                    else
                    {
                        _RTLBitrate = 0;
                        _powerPercent = 0;

                        // no data on input
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.Error(ex);
                    State = DriverStateEnum.Error;
                }
            }

            _loggingService.Info($"_dataWorker finished");
        }

        public string DeviceName
        {
            get
            {
                return $"{_deviceName} ({_magic})";
            }
        }

        public int Frequency
        {
            get
            {
                return _frequency;
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

        public double PowerPercent
        {
            get
            {
                return _powerPercent;
            }
        }

        public double Power
        {
            get
            {
                return _power;
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

        public void SendCommand(Command command)
        {
            _loggingService.Info($"Enqueue command: {command}");

            lock (_lock)
            {
                _commandQueue.Enqueue(command);
            }
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

            Settings.SDRSampleRate = sampleRate;
        }

        public void SetDirectSampling(int value)
        {
            _loggingService.Info($"Setting direct sampling: {value}");
            SendCommand(new Command(CommandsEnum.TCP_SET_DIRECT_SAMPLING, value));
        }

        public void SetGainMode(bool manual)
        {
            _loggingService.Info($"Setting {(manual ? "manual" : "automatic")} gain mode");

            Settings.AutoGain = !manual;

            SendCommand(new Command(CommandsEnum.TCP_SET_GAIN_MODE, (int) (manual ? 1 : 0)));
        }

        public void SetGain(int gain)
        {
            _loggingService.Info($"Setting gain: {gain}");

            Settings.Gain = gain;

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
