using LoggerService;
using NLog;
using RTLSDR;
using RTLSDR.DAB;
using RTLSDR.FM;
using System;
using System.IO;
using RTLSDR.Common;
using System.Data;
using System.Diagnostics;
using System.Collections;
using RTLSDR.Audio;

namespace RTLSDR.FMDAB.Console
{
    public class ConsoleApp
    {
        private ILoggingService _logger = null;
        private IRawAudioPlayer _audioPlayer = null;
        private bool _rawAudioPlayerInitialized = false;

        private ISDR _sdrDriver = null;
        private PowerCalculation _powerCalculator = null;
        private double _power = 0;

        private Stream _outputFileStream = null;
        private ConsoleAppParams _appParams = null;
        private int _totalDemodulatedDataLength = 0;
        private DateTime _demodStartTime;
        private IDemodulator _demodulator = null;
        private Wave _wave = null;
        public event EventHandler OnFinished = null;
        public event EventHandler OnDemodulated = null;
        bool _fileProcessed = false;
        private Stream _recordStream = null;

        public ConsoleApp(IRawAudioPlayer audioPalyer, ISDR sdrDriver, ILoggingService loggingService)
        {
            _audioPlayer = audioPalyer;
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
                fm.Emphasize = _appParams.FMEmphasize;

                _demodulator = fm;
            }
            if (_appParams.DAB)
            {
                var DABProcessor = new DABProcessor(_logger);
                DABProcessor.OnServiceFound += DABProcessor_OnServiceFound;
                DABProcessor.ServiceNumber = _appParams.ServiceNumber;
                /*
                DABProcessor.ProcessingSubChannel = new DABSubChannel()
                {
                    StartAddr = 570,
                    Length = 72, // 90
                    Bitrate = 96,
                    ProtectionLevel = EEPProtectionLevel.EEP_3
                };
                */
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


            System.Console.Write("Press ENTER to exit");
            System.Console.ReadLine();

            _logger.Debug("Exiting app");

            Stop();
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

            _sdrDriver.Init(new DriverInitializationResult()
            {
                OutputRecordingDirectory = "/temp"
            });
        }

        private void OutData(byte[] data, int size)
        {
            if (!String.IsNullOrEmpty(_appParams.OutputRawFileName))
            {
                _recordStream.Write(data, 0, size);
            }

            _demodulator.AddSamples(data, size);

     /*       if (_powerCalculator == null)
            {
                _powerCalculator = new PowerCalculation();
            }
            _power = _powerCalculator.GetPowerPercent(data, size);*/
        }

        private void ProcessFile()
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
        }

        private void DABProcessor_OnServiceFound(object sender, EventArgs e)
        {
            if (e is DABServiceFoundEventArgs dab)
            {
                System.Console.WriteLine($"   *** found service #{dab.Service.ServiceNumber.ToString().PadLeft(5,' ')} {dab.Service.ServiceName}");
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

                    if (_appParams.Play && (_audioPlayer != null))
                    {
                        if (!_rawAudioPlayerInitialized)
                        {
                            _audioPlayer.Init(ed.AudioDescription, _logger);
                            _audioPlayer.Play();
                        }

                        _audioPlayer.AddPCM(ed.Data);
                    }

                    if (OnDemodulated != null)
                    {
                        OnDemodulated(this, e);
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
            _fileProcessed = true;

            _rawAudioPlayerInitialized = false;

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
