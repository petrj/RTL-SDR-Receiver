using LoggerService;
using NLog;
using RTLSDR;
using RTLSDR.DAB;
using RTLSDR.FM;
using System;
using System.Linq;
using System.IO;
using RTLSDR.Common;
using System.Data;
using System.Diagnostics;
using System.Collections;
using RTLSDR.Audio;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RTLSDR.FMDAB.Console
{
    public class ConsoleApp
    {
        private ILoggingService _logger = null;
        private IRawAudioPlayer _audioPlayer = null;
        private bool _rawAudioPlayerInitialized = false;

        private ISDR _sdrDriver = null;

        private Stream _outputFileStream = null;
        private ConsoleAppParams _appParams = null;
        private int _totalDemodulatedDataLength = 0;
        private DateTime _demodStartTime;
        private IDemodulator _demodulator = null;
        private Wave _wave = null;
        public event EventHandler OnFinished = null;
        public event EventHandler OnDemodulated = null;

        private Stream _recordStream = null;
        private Dictionary<uint, DABService> _dabServices = new Dictionary<uint, DABService>();
        private Dictionary<uint, FMService> _FMServices = new Dictionary<uint, FMService>();

        private DABServicePlayedEventArgs _justPlaying = null;
        private DABServicePlayedEventArgs _justPlayingNotified = null;

        private List<byte> _fmTuningAudioBuffer = new List<byte>();
        private bool _fmTuning = false;

        private CancellationTokenSource? _fmTuneCts = null;
        private Task? _fmTuneTask = null;

        public ConsoleApp(IRawAudioPlayer audioPlayer, ISDR sdrDriver, ILoggingService loggingService)
        {
            _audioPlayer = audioPlayer;
            _logger = loggingService;
            _sdrDriver = sdrDriver;
            _appParams = new ConsoleAppParams("RTLSDR.FMDAB.Console");
        }

        public ConsoleAppParams Params
        {
            get { return _appParams; }
        }

        public void Run(string[] args)
        {
            if (!_appParams.ParseArgs(args))
            {
                return;
            }

            _logger.Info("DAB+/FM Console Radio Player");

            // test:
            //var aacDecoder = new AACDecoder(logger);
            //aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");

            if (_appParams.FM)
            {
                var fm = new FMDemodulator(_logger);
                fm.Mono = _appParams.Mono;

                _demodulator = fm;
            }
            if (_appParams.DAB)
            {
                var DABProcessor = new DABProcessor(_logger);
                DABProcessor.OnServiceFound += DABProcessor_OnServiceFound;
                DABProcessor.OnServicePlayed += DABProcessor_OnServicePlayed;
                DABProcessor.ServiceNumber = _appParams.ServiceNumber;
                _demodulator = DABProcessor;
            }

            if (!String.IsNullOrEmpty(_appParams.OutputRawFileName))
            {
                _recordStream = new FileStream(_appParams.OutputRawFileName, FileMode.Create, FileAccess.Write);
            }

            _demodulator.OnDemodulated += AppConsole_OnDemodulated;
            _demodulator.OnFinished += AppConsole_OnFinished;

            switch (_appParams.InputSource)
            {
                case InputSourceEnum.File:
                    ProcessFile();
                    break;
                case (InputSourceEnum.RTLDevice):
                    ProcessDriverData();
                    break;
                default:
                    _logger.Info("Unknown source");
                    break;
            }

            ConsoleUILoop();

            _logger.Debug("Exiting app");
            Stop();
        }

        private void ConsoleUILoop()
        {
            var finish = false;
            while (!finish)
            {
                System.Console.WriteLine();
                System.Console.Write("RTLSDR.FMDAB.Console");
                if ((_demodulator != null) && (_demodulator is DABProcessor db))
                {
                    if (db.ServiceNumber >= 0)
                    {
                        if (db.ProcessingSubCannel == null || db.ProcessingDABService == null)
                        {
                            System.Console.Write($" - searching service # {db.ServiceNumber}....");
                        } else
                        {
                            System.Console.Write($" - playing # {db.ProcessingDABService.ServiceNumber} {db.ProcessingDABService.ServiceName} ({db.ProcessingSubCannel.Bitrate} KHz)");
                        }
                    }
                }
                System.Console.WriteLine();
                System.Console.WriteLine();

                System.Console.WriteLine(" Console commands:");
                if (_appParams.DAB)
                {
                    System.Console.WriteLine("  service_number          play audio service by given number");
                }
                System.Console.WriteLine("  frequency               FM frequency");
                System.Console.WriteLine("                          '104 000 000'");
                System.Console.WriteLine("                          '96.9 MHz'");
                System.Console.WriteLine("  i                       info - show audio services");
                System.Console.WriteLine("  t                       start/stop tune (FM only)");
                System.Console.WriteLine("  q                       quit");


                System.Console.WriteLine();
                System.Console.Write("Enter command:");
                var command = System.Console.ReadLine();
                if (command == "q")
                {
                    finish = true;
                    continue;
                }

                if (command == "")
                {
                    continue;
                }

                if (command == "t")
                {
                    if (_appParams.FM)
                    {
                        if (_fmTuneTask != null)
                        {
                            StopFMTune();
                        } else
                        {
                            StartFMTune();
                        }                        
                    }
                }

                if (command == "i")
                {
                    if (_appParams.DAB)
                    {
                        System.Console.WriteLine("DAB services:");
                        foreach (var service in _dabServices)
                        {
                            System.Console.WriteLine($"# {service.Value.ServiceNumber.ToString().PadLeft(6, ' ')} {service.Value.ServiceName} ({service.Value.FirstSubChannel.Bitrate} KHz)");
                        }
                    }
                    if (_appParams.FM)
                    {
                        System.Console.WriteLine($"Current frequency: {_sdrDriver.Frequency}");
                        ShowFMServices();
                    }
                    continue;
                }

                
                if (_appParams.DAB)
                {                  
                    int serviceNumber;
                    if (int.TryParse(command, out serviceNumber))
                    {
                        if (_dabServices.ContainsKey(Convert.ToUInt32(serviceNumber)))
                        {
                            var dabProcessor = (_demodulator as DABProcessor);
                            var service = _dabServices[Convert.ToUInt32(serviceNumber)];
                            var channel = service.FirstSubChannel;

                            System.Console.WriteLine($"Setting service number \"{serviceNumber}\" ");

                            dabProcessor.SetProcessingSubChannel(service, channel);
                            continue;
                        }                        
                    }                    
                }

                if (_appParams.FM)
                {
                    uint serviceNumber;
                    if (uint.TryParse(command, out serviceNumber))
                    {
                        if (_FMServices.ContainsKey(serviceNumber))
                        {
                             System.Console.WriteLine($"Setting service number \"{serviceNumber}\" ");

                            _sdrDriver.SetFrequency(Convert.ToInt32(_FMServices[serviceNumber].Frequency*1000000));
                            continue;
                        }                        
                    }
                }

                // change frequency?
                var freq = AudioTools.ParseFreq(command);
                if (freq > 0)
                {
                    System.Console.Write($"Setting frequency {freq}");
                    _sdrDriver.SetFrequency(freq);

                    if (_demodulator is DABProcessor dab)
                    {
                        dab.ServiceNumber = -1;
                    }

                    _audioPlayer.ClearBuffer();                    
                }
            }

            System.Console.Write("RTLSDR.FMDAB.Console UI loop finished");
        }

        void StartFMTune()
        {
            _fmTuneCts = new CancellationTokenSource();

            _fmTuneTask = Task.Run(() =>
            {
                FMTune();
            });

            System.Console.WriteLine($"Tuning has been started");
        }

        void StopFMTune()
        {
            if (_fmTuneCts is null)
                return;

            _fmTuneCts.Cancel();
            _fmTuneCts.Dispose();
            _fmTuneCts = null;
            if (_fmTuneTask != null)
            {             
                _fmTuneTask.Dispose();
                _fmTuneTask = null;
            }

            System.Console.WriteLine($"Tuning has been stopped");
        }

        private async Task FMTune()
        {
            System.Console.Write("FM Tuning");

            _FMServices.Clear();

            try
            {
                var startFreqFMMhz = 88.0;
                var endFreqFMMhz = 108.0;
                var bandWidthMhz = 0.1;

                var tuneDelaMS_1 = 300;  // wait for freq change
                var tuneDelaMS_2 = 500;  // wait for buffer fill
                var tuneDelaMS_3 = 1000; // hear 85
                var tuneDelaMS_4 = 5000; // hear 90

                _fmTuning = true;
                uint n = 1;
                for (var f = startFreqFMMhz; f < endFreqFMMhz; f += bandWidthMhz)
                {
                    if (_fmTuneCts == null || _fmTuneCts.IsCancellationRequested)
                    {
                        break;
                    }

                    var freq = AudioTools.ParseFreq($"{f}Mhz");
                    System.Console.WriteLine($"Freq:{freq} ... ");

                    _sdrDriver.SetFrequency(freq);
                    
                    //var isStation = FMStereoDecoder.IsStationPresent()

                    //_demodulator.isStation

                    await Task.Delay(tuneDelaMS_1); // wait for freq change
                    
                    _audioPlayer.ClearBuffer();
                    _fmTuningAudioBuffer.Clear();
                    
                    await Task.Delay(tuneDelaMS_2); // wait for buffer fill
                    
                    var stationPresentPercents = AudioTools.IsStationPresent(_fmTuningAudioBuffer.ToArray());

                    if (stationPresentPercents>85)
                    {
                        _FMServices.Add( n, new FMService()
                        {
                             Frequency = f,
                             StationPercents = stationPresentPercents
                        });

                         ShowFMServices();

                         await Task.Delay(stationPresentPercents>90 ? tuneDelaMS_4 : tuneDelaMS_3); // hear
                         n++;
                    }
                    //System.Console.WriteLine($"        ({stationPresentPercents.ToString("N2")})");
                }

            }
            catch (OperationCanceledException)
            {
                // Expected exit path — not an error
            }            
            finally
            {
                _fmTuning = false;
            }
        }

        private void ShowFMServices()
        {
            System.Console.WriteLine("--------------------------------------------------");
            foreach (var kvp in _FMServices)
            {
                System.Console.WriteLine($"{kvp.Key.ToString().PadLeft(4,' ')}                     {kvp.Value.Frequency.ToString("N1")} MHz     {kvp.Value.StationPercents.ToString("N1")}%");
            }
            System.Console.WriteLine("--------------------------------------------------");
        }        

        private void ProcessDriverData()
        {
            // FM
            _sdrDriver.SetFrequency(_appParams.Frequency);
            _sdrDriver.SetSampleRate(_appParams.SampleRate);

            // DAB 7C
            //_sdrDriver.SetFrequency(192352000);
            //_sdrDriver.SetSampleRate(2048000);

            _sdrDriver.OnDataReceived += (sender, onDataReceivedEventArgs) =>
            {
                OutData(onDataReceivedEventArgs.Data, onDataReceivedEventArgs.Size);
            };

            foreach (var process in Process.GetProcessesByName("rtl_tcp"))
            {
                try
                {
                    System.Console.WriteLine($"Killilng running rtl_tcp process {process.Id}"); 
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                    
                } catch (Exception ex)
                {
                    System.Console.WriteLine(ex);                    
                }
            }
            var processRunning = Process.GetProcessesByName("rtl_tcp").Any();

            _sdrDriver.Init(new DriverInitializationResult()
            {
                OutputRecordingDirectory = "/temp"
            });


            //_sdrDriver.SetGain(0);
            //_sdrDriver.SetIfGain(true);
            //_sdrDriver.SetAGCMode(true);
            //_sdrDriver.SetGainMode(true);
        }

        private void OutData(byte[] data, int size)
        {
            if (!String.IsNullOrEmpty(_appParams.OutputRawFileName))
            {
                _recordStream.Write(data, 0, size);
            }

            _demodulator.AddSamples(data, size);
        }

        private void ProcessFile()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                var bufferSize = 65535; //1024 * 1024;
                var IQDataBuffer = new byte[bufferSize];

                _demodStartTime = DateTime.Now;
                var lastBufferFillNotify = DateTime.MinValue;

                using (var inputFs = new FileStream(_appParams.InputFileName, FileMode.Open, FileAccess.Read))
                {
                    _logger.Info($"Total bytes : {inputFs.Length}");
                    long totalBytesRead = 0;

                    while (inputFs.Position < inputFs.Length)
                    {
                        var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);
                        totalBytesRead += bytesRead;

                        if ((DateTime.Now - lastBufferFillNotify).TotalMilliseconds > 1000)
                        {
                            lastBufferFillNotify = DateTime.Now;
                            if (inputFs.Length > 0)
                            {
                                var percents = totalBytesRead / (inputFs.Length / 100);
                                _logger.Debug($" Processing input file:                   {percents} %");
                            }
                        }

                        OutData(IQDataBuffer, bytesRead);

                        System.Threading.Thread.Sleep(25);
                    }
                }
            });
        }

        private void DABProcessor_OnServicePlayed(object sender, EventArgs e)
        {
            if (e is DABServicePlayedEventArgs pl)
            {
                _justPlaying = pl;
                if (_justPlayingNotified != _justPlaying)
                {
                    System.Console.WriteLine($"Playing #{_justPlaying.Service.ServiceNumber} {_justPlaying.Service.ServiceName} ({_justPlaying.SubChannel.Bitrate} KHz)");
                    _justPlayingNotified = _justPlaying;
                }
            }
        }

        private void DABProcessor_OnServiceFound(object sender, EventArgs e)
        {
            if (e is DABServiceFoundEventArgs dab)
            {
                if (!_dabServices.ContainsKey(dab.Service.ServiceNumber))
                {
                    _dabServices.Add(dab.Service.ServiceNumber, dab.Service);
                    System.Console.WriteLine($"   Service found:  #{dab.Service.ServiceNumber.ToString().PadLeft(5, ' ')} {dab.Service.ServiceName}");
                }
            }
        }

        private void AppConsole_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                _totalDemodulatedDataLength += ed.Data.Length;

                try
                {
                    if (_appParams.OutputToFile)
                    {
                        if (_wave == null)
                        {
                            _wave = new Wave();
                            _wave.CreateWaveFile(_appParams.OutputFileName, ed.AudioDescription);
                            //_outputFileStream = new FileStream(_appParams.OutputFileName, FileMode.Create, FileAccess.Write);
                        }

                        _wave.WriteSampleData(ed.Data);
                        //_outputFileStream.Write(ed.Data, 0, ed.Data.Length);
                    }

                    if (_audioPlayer != null)
                    {
                        if (!_rawAudioPlayerInitialized)
                        {
                            _audioPlayer.Init(ed.AudioDescription, _logger);

                            Task.Run(async () =>
                            {
                                await Task.Delay(1000);  // fill buffer
                                _audioPlayer.Play();
                            });

                            _rawAudioPlayerInitialized = true;
                        }

                        _audioPlayer.AddPCM(ed.Data);
                    }

                    if (OnDemodulated != null)
                    {
                        OnDemodulated(this, e);
                    }

                    if (_fmTuning)
                    {
                        _fmTuningAudioBuffer.AddRange(ed.Data);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }
        }

        public void Stop()
        {

            if (_demodulator is DABProcessor dab)
            {
                foreach (var service in dab.FIC.Services)
                {
                    _logger.Info($"{Environment.NewLine}{service}");
                }

                dab.Stop();
                dab.Stat(true);
            }

            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
            }
            _rawAudioPlayerInitialized = false;

            if (_appParams.OutputToFile)
            {
                if (_wave != null)
                {
                    _wave.CloseWaveFile();
                }

                //_outputFileStream.Flush();
                //_outputFileStream.Close();
                //_outputFileStream.Dispose();

                _logger.Info($"Saved to                     : {_appParams.OutputFileName}");
            }

            if (_appParams.OutputToFile || _appParams.StdOut)
            {
                _logger.Info($"Total demodulated data size  : {_totalDemodulatedDataLength} bytes");
            }

            if (_sdrDriver != null)
            {
                _sdrDriver.Disconnect();
            }

            if (OnFinished != null)
            {
                OnFinished(this, new EventArgs());
            }
        }

        private void AppConsole_OnFinished(object sender, EventArgs e)
        {
            Stop();
        }
    }
}
