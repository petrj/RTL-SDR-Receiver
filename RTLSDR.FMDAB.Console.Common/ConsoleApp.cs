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

namespace RTLSDR.FMDAB.Console.Common
{
    public class ConsoleApp
    {
        ILoggingService _logger = null;
        private Stream _outputFileStream = null;
        private ConsoleAppParams _appParams = null;
        private int _totalDemodulatedDataLength = 0;
        private DateTime _demodStartTime;
        private IDemodulator _demodulator = null;
        private Stream _stdOut = null;
        private Wave _wave = null;

        public event EventHandler OnFinished = null;
        public event EventHandler OnDemodulated = null;

        private static ISDR _sdrDriver = null;

        bool _fileProcessed = false;

        public ConsoleApp(string appName)
        {
            _appParams = new ConsoleAppParams(appName);
        }

        public ConsoleAppParams Params
        {
            get { return _appParams; }
        }

        public ILoggingService Logger
        {
            get { return _logger; }
        }

        public void Run(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (!_appParams.ParseArgs(args))
            {
                return;
            }

            var logConfigPath = _appParams.StdOut ? "NLog.UDP.config" : "NLog.config";
            _logger = new NLogLoggingService(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logConfigPath));

            _logger.Info("DAB+/FM Console Radio Player");

            // test:
            //var aacDecoder = new AACDecoder(logger);
            //aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");

            if (_appParams.StdOut)
            {
                _stdOut = System.Console.OpenStandardOutput();
            }

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

            _demodulator.OnDemodulated += AppConsole_OnDemodulated;
            _demodulator.OnFinished += AppConsole_OnFinished;

            //_sdrDriver = new RTLSDR.RTLSRDTestDriver(_logger);

            _sdrDriver = new RTLSDR.RTLTCPIPDriver(_logger);
            _sdrDriver.OnDataReceived += (sender, onDataReceivedEventArgs) =>
            {
                _demodulator.AddSamples(onDataReceivedEventArgs.Data, onDataReceivedEventArgs.Size);
            };

            _sdrDriver.Init(new DriverInitializationResult()
            {
                OutputRecordingDirectory = "/temp"
            });
            
            _demodulator.Finish();
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

                if (_stdOut != null)
                {
                    _stdOut.Write(ed.Data, 0, ed.Data.Length);
                    _stdOut.Flush();
                }

                if (OnDemodulated != null)
                {
                    OnDemodulated(this, e);
                }
            }
        }

        private void AppConsole_OnFinished(object sender, EventArgs e)
        {
            _fileProcessed = true;

            if (_demodulator is DABProcessor dab)
            {
                foreach (var service in dab.FIC.Services)
                {
                    _logger.Info($"{Environment.NewLine}{service}");
                }

                dab.Stop();
                dab.Stat(true);
            }

            if (_stdOut != null)
            {
                _stdOut.Flush();
                _stdOut.Close();
                _stdOut.Dispose();
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

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            System.Console.WriteLine((e.ExceptionObject as Exception).Message);

            if (_logger != null)
            {
                _logger.Error(e.ExceptionObject as Exception);
            }
        }
    }
}
