using LoggerService;
using RTLSDR.Common;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RTLSDR
{

    public class RTLTCPIPDriver : ISDR
    {
        private ILoggingService _loggingService;

        private int _frequency = 192352000;
        private int _sampleRate = 2048000;

        private double _bitrate = 0;

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

        public int _gain = 0;

        private Process _process;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;

        public RTLTCPIPDriver(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public string DeviceName
        {
            get
            {
                return "RTL SDR TCPIP driver";
            }
        }

        public DriverSettings Settings { get; private set; } = new DriverSettings();

        public bool? Installed { get; set; } = true;

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
                return TunerTypeEnum.RTLSDR_TUNER_UNKNOWN;
            }
        }

        public long RTLBitrate
        {
            get
            {
                return Convert.ToInt64(_bitrate);
            }
        }

        public double PowerPercent
        {
            get
            {
                return 100;
            }
        }

        public double Power
        {
            get
            {
                return 1;
            }
        }

        public event EventHandler<OnDataReceivedEventArgs> OnDataReceived;

        public void Disconnect()
        {
            _loggingService.Info($"Disconnecting driver");

            _cts?.Cancel();

            _process?.Kill(true);
        }

        public void SetErrorState()
        {
            _loggingService.Info($"Setting manually error state");
            State = DriverStateEnum.Error;
        }

        public void Run(string command, string args, ILoggingService loggerService, string workingDir=null)
        {
            try
            {
                _process = new System.Diagnostics.Process();

                _process.OutputDataReceived += (sender, a) =>
                {
                    loggerService.Info($"rtl_tcp: {a.Data}");
                };

                _process.StartInfo.FileName = workingDir == null ? command : Path.Combine(workingDir, command);
                _process.StartInfo.UseShellExecute = false;
                _process.StartInfo.Arguments = args;
                //_process.StartInfo.CreateNoWindow = true;
                _process.StartInfo.RedirectStandardOutput = true;
                if (workingDir != null)
                {
                    _process.StartInfo.WorkingDirectory = workingDir;
                }
                //_process.EnableRaisingEvents = true;
                _process.Start();
                _process.BeginOutputReadLine();
                _process.WaitForExit();
                _process.CancelOutputRead();
            }
            catch (Exception ex)
            {
                loggerService.Error(ex);
            }
        }

        public async Task ConnectAsync(string host = "127.0.0.1", int port = 1234)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();

            // Skip initial rtl_tcp header (12 bytes: "RTL0" + tuner + gain count + gains)
            byte[] header = new byte[12];
            await _stream.ReadAsync(header, 0, header.Length);
            _loggingService.Info("Connected to rtl_tcp.");
        }

        private void StartRTlSDRProcess()
        {
            Task.Run(() =>
            {
                State = DriverStateEnum.Connected;
                Run("rtl_tcp", $"-f {Frequency} -s {_sampleRate} -g {_gain}", _loggingService);
            });
        }

        private async Task RunDriverMainLoop()
        {
            try
            {
                var bufferSize = 65535;
                var buffer = new byte[bufferSize];

                _stream.ReadTimeout = 500;

                while (!_cts.Token.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                    if (bytesRead <= 0) break;

                    if (OnDataReceived != null)
                    {
                        //Console.WriteLine($"data received: {bytesRead.ToString().PadLeft(20, ' ')} bytes");
                        OnDataReceived(this, new OnDataReceivedEventArgs()
                        {
                            Data = buffer,
                            Size = bytesRead
                        });
                    }
                }

            }
            catch (IOException s)
            {
                if (State == DriverStateEnum.DisConnected)
                {
                    return;  // stream.Read when disconnected?
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                State = DriverStateEnum.DisConnected;
                _loggingService.Error(ex);
            } finally
            {
                _loggingService.Info("Read loop finished");
            }
        }

        public void Init(DriverInitializationResult driverInitializationResult)
        {
            _loggingService.Info($"Starting {DeviceName}");

            StartRTlSDRProcess();

            Task.Run(
                async () =>
                {
                    _loggingService.Info("Waiting 5 secs for init driver");
                    await Task.Delay(5000);

                    await ConnectAsync();

                    await RunDriverMainLoop();
                });
        }

        public void SendCommand(Command command)
        {

        }

        public void SetAGCMode(bool automatic)
        {

        }

        public void SetDirectSampling(int value)
        {

        }

        public void SetFrequency(int freq)
        {
            if (_frequency != freq)
            {
                _frequency = freq;

                //Disconnect();
                //RunDriverMainLoop();
            }
        }

        public void SetFrequencyCorrection(int correction)
        {

        }

        public void SetGain(int gain)
        {
            _gain = gain;
        }

        public void SetGainMode(bool manual)
        {

        }

        public void SetIfGain(bool ifGain)
        {

        }

        public void SetSampleRate(int sampleRate)
        {
            _sampleRate = sampleRate;
        }
    }

}