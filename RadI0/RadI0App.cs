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
using NAudio.MediaFoundation;
using Terminal.Gui;
using System.Net;

namespace RadI0;

public class RadI0App
{
    private ILoggingService _logger;
    private IRawAudioPlayer _audioPlayer;
    private object _lock = new object();
    private ISDR _sdrDriver;
    private ConsoleAppParams _appParams;
    private int _processingFilePercents = 0;
    private string _processingFileBitRate = "";

    private bool _rawAudioPlayerInitialized = false;
    public event EventHandler OnDemodulated = null;
    public event EventHandler OnFinished = null;

    private IDemodulator? _demodulator = null;

    private List<Station> _stations = new List<Station>();

    private Rad10GUI _gui;

    public RadI0App(IRawAudioPlayer audioPlayer, ISDR sdrDriver, ILoggingService loggingService, Rad10GUI gui)
    {
        _gui = gui;
        _audioPlayer = audioPlayer;
        _logger = loggingService;
        _sdrDriver = sdrDriver;
        _appParams = new ConsoleAppParams("Rad10");

        _gui.OnStationChanged += StationChanged;
        _gui.OnGainChanged += GainChanged;
        _gui.OnFrequentionChanged += FrequentionChanged;
        _gui.OnQuit += OnQuit;
    }

    private void OnQuit(object sender, EventArgs e)
    {
        if (_demodulator != null)
        {
            _demodulator.Stop();
        }

        if (_sdrDriver != null)
        {
            if (_sdrDriver.State == DriverStateEnum.Connected)
            {
                _sdrDriver.Disconnect();
            }
        }
    }

    private void StationChanged(object sender, EventArgs e)
    {
        if (e is StationFoundEventArgs d)
        {
            Play(d.Station);
        }
    }

    private void GainChanged(object sender, EventArgs e)
    {
        if (e is GainChangedEventArgs d)
        {
            _appParams.HWGain = d.HWGain;
            _appParams.AutoGain = d.SWGain;
            _appParams.Gain = d.ManualGainValue;

            SetGain();
        }
    }

    private void FrequentionChanged(object sender, EventArgs e)
    {
        if (e is FrequentionChangedEventArgs d)
        {
            _appParams.Frequency = d.Frequention;
            _sdrDriver.SetFrequency(_appParams.Frequency);
        }
    }

    public async Task StartAsync(string[] args)
    {
        if (!_appParams.ParseArgs(args))
        {
            return;
        }

        _logger.Info("DAB+/FM Radio Player");

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

        //if (!String.IsNullOrEmpty(_appParams.OutputRawFileName))
        //{
        //    _recordStream = new FileStream(_appParams.OutputRawFileName, FileMode.Create, FileAccess.Write);
        //}

        if (_demodulator != null)
        {
            _demodulator.OnDemodulated += AppConsole_OnDemodulated;
            _demodulator.OnFinished += AppConsole_OnFinished;
        }

        Task.Run( async () =>
        {
            await RefreshGUILoop();
        });

        switch (_appParams.InputSource)
        {
            case InputSourceEnum.File:
                await ProcessFile();
                break;
            case (InputSourceEnum.RTLDevice):
                ProcessDriverData();
                break;
            default:
                _logger.Info("Unknown source");
                break;
        }

        _logger.Debug("Rad10 Run method finished");
    }

    private string GetState()
    {
        if (_sdrDriver == null)
        {
            return "Not initialized";
        }

        switch (_sdrDriver.State)
        {
            case DriverStateEnum.NotInitialized:
                return "Not initialized";
            case DriverStateEnum.Connected:
                return "Connected";
            case DriverStateEnum.DisConnected:
                return "DisConnected";
            case DriverStateEnum.Error:
                return "Error";
        }

        return "??";
    }

    public static string GetFrequencyForDisplay(int freq)
    {
        var dabFreq = "";
        foreach (var df in AudioTools.DabFrequenciesHz)
        {
            if (df.Value == freq)
            {
                dabFreq = df.Key;
                break;
            }
        }
        var  frequency = $"{(freq / 1000000.0).ToString("N3")} MHz";

        if (dabFreq != "")
        {
            frequency = $"{dabFreq} ({frequency})";
        }

        return frequency;
    }

    private async Task RefreshGUILoop()
    {
        while (true)
        {
            string status = "";
            string bitRate = "";
            string frequency = "";
            string device = "";
            string audio = "";
            bool synced = false;

            switch (_appParams.InputSource)
            {
                case InputSourceEnum.File:
                    device = _appParams.InputFileName;
                    status = $"Reading file: {_processingFilePercents.ToString().PadLeft(3,' ')}%";
                    bitRate = _processingFileBitRate;
                    break;
                case (InputSourceEnum.RTLDevice):
                    device = _sdrDriver.DeviceName;
                    bitRate = $"{(_sdrDriver.RTLBitrate / 1000000.0).ToString("N1")} MB/s";
                    frequency = GetFrequencyForDisplay(_sdrDriver.Frequency);
                    status = GetState();
                    break;
            }

            if (_demodulator != null)
            {
                synced = _demodulator.Synced;
            }

            if (_audioPlayer != null)
            {
                var audioDesc = _audioPlayer.GetAudioDataDescription();

                if (audioDesc != null)
                {
                    if (audioDesc.Channels == 1)
                    {
                        audio = "Mono";
                    } else
                    if (audioDesc.Channels == 2)
                    {
                        audio = "Stereo";
                    } else
                    {
                        audio = $"{audioDesc.Channels} chs";
                    }

                    audio += $", {audioDesc.BitsPerSample}b, {audioDesc.SampleRate/1000} KHz";
                }
            }

            var gain = "";
            if (_appParams.HWGain)
            {
                gain = "HW";
            } else
            if (_appParams.AutoGain)
            {
                gain = $"SW ({(_sdrDriver.Gain / 10.0).ToString("N1")} dB)";
            } else
            {
                gain = $"{(_sdrDriver.Gain / 10.0).ToString("N1")} dB";
            }

            var audioBitRate = "";
            if (_demodulator != null)
            {
                audioBitRate =  $"{(_demodulator.AudioBitrate / 1000.0).ToString("N0")} KB/s";
            }

            _gui.RefreshBand(_appParams.FM);
            _gui.RefreshStat(status,bitRate,frequency,device,audio,
            synced ? "[x]" : "[ ]",
            gain, audioBitRate);

            await Task.Delay(500);
        }
    }

    public List<Station> Stations
    {
         get => _stations; set => _stations = value;
    }

    private Station? GetStationByServiceNumber(int serviceNumber)
    {
        lock(_lock)
        {
            foreach (var station in _stations)
            {
                if (station.ServiceNumber == serviceNumber)
                {
                    return station;
                }
            }
            return null;
        }
    }

    private void DABProcessor_OnServiceFound(object sender, EventArgs e)
    {
        if (e is DABServiceFoundEventArgs dab)
        {
            var snum = Convert.ToInt32(dab.Service.ServiceNumber);
            var st = GetStationByServiceNumber(snum);
            if (st == null)
            {
                // new station
                st = new Station(dab.Service.ServiceName,snum);
                st.Service = dab.Service;
                lock(_lock)
                {
                    _stations.Add(st);
                }

                Station playingStation = null;
                if (_demodulator is DABProcessor dp && dp.ServiceNumber != -1)
                {
                    playingStation = GetStationByServiceNumber(dp.ServiceNumber);
                }

                _gui.RefreshStations(_stations, playingStation);
            }

            // autoplay
            if (_demodulator is DABProcessor dabs)
            {
                    if (dabs.ServiceNumber == -1)
                    {
                        dabs.ServiceNumber = Convert.ToInt32(dab.Service.ServiceNumber);
                    }

                    if (dabs.ServiceNumber != dab.Service.ServiceNumber)
                    {
                        return;
                    }

                    Task.Run(async () =>
                    {
                        _logger.Debug($"Autoplay \"{dab.Service.ServiceName}\"");
                        await Task.Delay(2000);
                        //var channel = dab.Service.FirstSubChannel;
                        //dabs.SetProcessingSubChannel(dab.Service, channel);
                        Play(st);

                         _gui.RefreshStations(_stations, GetStationByServiceNumber(dabs.ServiceNumber));
                    });
            }
        }
    }

    private void Play(Station station)
    {
        if (_demodulator is DABProcessor dabs)
        {
            var service = station.Service;
             dabs.SetProcessingService(service);
        }
    }

    private void DABProcessor_OnServicePlayed(object sender, EventArgs e)
    {
        if (e is DABServicePlayedEventArgs pl)
        {
            /*
            _justPlaying = pl;
            if (_justPlayingNotified != _justPlaying && _justPlaying.SubChannel != null)
            {
                System.Console.WriteLine($"Playing #{_justPlaying.Service.ServiceNumber} {_justPlaying.Service.ServiceName} ({_justPlaying.SubChannel.Bitrate} KHz)");
                _justPlayingNotified = _justPlaying;
            }
            */
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

            //_totalDemodulatedDataLength += ed.Data.Length;

            try
            {
                /*
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
                */

                if (_audioPlayer != null)
                {
                    if (!_rawAudioPlayerInitialized)
                    {
                        _audioPlayer.Init(ed.AudioDescription, _logger);

                        Task.Run(async () =>
                        {
                            await Task.Delay(500);  // fill buffer
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

                //if (_fmTuning)
                //{
                //    _fmTuningAudioBuffer.AddRange(ed.Data);
                //}
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }
    }

    private void AppConsole_OnFinished(object sender, EventArgs e)
    {
        Stop();
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

        /*
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
        */

        if (_sdrDriver != null)
        {
            _sdrDriver.Disconnect();
        }

        if (OnFinished != null)
        {
            OnFinished(this, new EventArgs());
        }
    }

    private void OutData(byte[] data, int size)
    {
        /*
        if (!String.IsNullOrEmpty(_appParams.OutputRawFileName))
        {
            _recordStream.Write(data, 0, size);
        }
        */

        _demodulator?.AddSamples(data, size);
    }

    public bool KillAnyProcess(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                //System.Console.WriteLine($"Killing running {process} process {process.Id}");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(2000);
                process.Close();
                process.Dispose();

            } catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        return !Process.GetProcessesByName(processName).Any();
    }

    private async Task ProcessDriverData()
    {
        // FM
        _sdrDriver.SetFrequency(_appParams.Frequency);
        _sdrDriver.SetSampleRate(_appParams.SampleRate);

        _sdrDriver.OnDataReceived += (sender, onDataReceivedEventArgs) =>
        {
            OutData(onDataReceivedEventArgs.Data, onDataReceivedEventArgs.Size);
        };

        var noProcessRunning = KillAnyProcess("rtl_tcp");
        if (!noProcessRunning)
        {
            _logger.Error("rtl_tcp is still running!");
        }

        await _sdrDriver.Init(new DriverInitializationResult()
        {
            OutputRecordingDirectory = "/temp"
        });

       SetGain();
    }

    private void SetGain()
    {
        if (_appParams.HWGain)
        {
            _sdrDriver.SetGain(0);
            _sdrDriver.SetGainMode(false);
            _sdrDriver.SetIfGain(true);
            _sdrDriver.SetAGCMode(true);
        } else
        {
            // always manual
            _sdrDriver.SetGainMode(true);

            if (_appParams.AutoGain)
            {
                _sdrDriver.SetGain(0);
                Task.Run( async () => await _sdrDriver.AutoSetGain());
            } else
            {
                _sdrDriver.SetGain(_appParams.Gain);
            }
        }
    }

    private async Task ProcessFile()
    {
        _processingFilePercents = 0;

        await System.Threading.Tasks.Task.Run(() =>
        {
            var bitRateCalculator = new BitRateCalculation(_logger, "Read file");
            var bufferSize = 65535; //1024 * 1024;
            var IQDataBuffer = new byte[bufferSize];

            var lastBufferFillNotify = DateTime.MinValue;

            using (var inputFs = new FileStream(_appParams.InputFileName, FileMode.Open, FileAccess.Read))
            {
                _logger.Info($"Total bytes : {inputFs.Length}");
                long totalBytesRead = 0;

                while (inputFs.Position < inputFs.Length)
                {
                    var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);
                    totalBytesRead += bytesRead;
                    bitRateCalculator.UpdateBitRate(bytesRead);
                    _processingFileBitRate = bitRateCalculator.BitRateAsShortString;

                    if ((DateTime.Now - lastBufferFillNotify).TotalMilliseconds > 1000)
                    {
                        lastBufferFillNotify = DateTime.Now;
                        if (inputFs.Length > 0)
                        {
                            var percents = totalBytesRead / (inputFs.Length / 100);
                            _logger.Debug($" Processing input file:                   {percents} %");
                            _processingFilePercents = Convert.ToInt32(percents);
                        }
                    }

                    OutData(IQDataBuffer, bytesRead);

                    System.Threading.Thread.Sleep(25);
                }
            }
        });
    }
}
