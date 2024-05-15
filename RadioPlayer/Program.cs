using LoggerService;
using RTLSDRConsole;
using RTLSDR.DAB;
using RTLSDR.Core;
using RTLSDR.FM;


  new MainClass().Run(args);

  public class MainClass
  {
    ILoggingService logger = new NLogLoggingService(System.IO.Path.Join(AppDomain.CurrentDomain.BaseDirectory,"NLog.config"));

    Stream _outputStream = null;
    AppParams _appParams;
    int _totalDemodulatedDataLength = 0;
    DateTime _demodStartTime;
    IDemodulator _demodulator = null;

    bool _fileProcessed = false;

        private bool ParseArgs(string[] args)
        {
            if (args.Length == 0)
            {
                ShowError("No param specified");
                return true;
            }

            foreach (var arg in args)
            {
                var p = arg.ToLower();
                if (p.StartsWith("--", StringComparison.InvariantCulture))
                {
                    p = p.Substring(1);
                }

                if (p == "-help")
                {
                    _appParams.Help = true;
                }
                else
                if (p == "-fm")
                {
                    _appParams.FM = true;
                } else
                if (p == "-dab")
                {
                    _appParams.DAB = true;
                } else
                if (p == "-e")
                {
                    _appParams.Emphasize = true;
                } else
                {
                    _appParams.InputFileName = arg;
                }
            }

            if (!_appParams.Help && string.IsNullOrEmpty(_appParams.InputFileName))
            {
                ShowError($"Input file not specified");
                return true;
            }

            if (!_appParams.Help && !File.Exists(_appParams.InputFileName))
            {
                ShowError($"Input file {_appParams.InputFileName} does not exist");
                return true;
            }

            if (_appParams.Help)
            {
                Help();
                return true;
            }

            if (!_appParams.FM && !_appParams.DAB)
            {
                ShowError("Missing param");
                return true;
            }

            return false;
        }

        private void Help()
        {
            Console.WriteLine("RTLSDRConsole.exe [option] [input file]");
            Console.WriteLine();
            Console.WriteLine("FM/DAB demodulator");
            Console.WriteLine();
            Console.WriteLine(" input file: unsigned 8 bit integers (uint8 or u8) from rtl_sdr");
            Console.WriteLine();
            Console.WriteLine(" options: ");
            Console.WriteLine(" -fm  \t FM demodulation");
            Console.WriteLine(" -dab \t DAB demodulation");
            Console.WriteLine(" -e   \t emphasize");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("example:");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -fm file.iq:");
            Console.WriteLine(" -> output is raw mono 16bit file.iq.output");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -dab file.iq:");
            Console.WriteLine(" -> output ???");
        }

        private void ShowError(string text)
        {
            Console.WriteLine($"Error. {text}. See help:");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -help");
            Console.WriteLine();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            Console.WriteLine((e.ExceptionObject as Exception).Message);
        }


        private void Program_OnFinished(object sender, EventArgs e)
        {
            _fileProcessed = true;

            if (_demodulator is DABProcessor dab)
            {
                foreach (var service in dab.FIC.Services)
                {
                    logger.Info($"{Environment.NewLine}{service}");
                }

                dab.StopThreads();
                dab.Stat(true);
            }

            _outputStream.Flush();
            _outputStream.Close();

            logger.Info($"Saved to                     : {_appParams.OutputFileName}");
            logger.Info($"Total demodulated data size  : {_totalDemodulatedDataLength} bytes");
        }

        private void Program_OnDemodulated(object sender, EventArgs e)
        {
            if (e is DataDemodulatedEventArgs ed)
            {
                if (ed.Data == null || ed.Data.Length == 0)
                {
                    return;
                }

                _totalDemodulatedDataLength += ed.Data.Length;
                _outputStream.Write(ed.Data, 0, ed.Data.Length);
            }
        }

    public void Run(string[] args)
    {

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            logger.Info("RTL SDR Test Console");

            if (ParseArgs(args))
            {
                return;
            }

            // test:
            //var aacDecoder = new AACDecoder(logger);
            //aacDecoder.Test("c:\\temp\\AUData.1.aac.superframe");

        _outputStream = new FileStream(_appParams.OutputFileName, FileMode.Create, FileAccess.Write);

            if (_appParams.FM)
            {
                var fm = new FMDemodulator(logger);
                fm.Emphasize = _appParams.Emphasize;

                _demodulator = fm;
            }
            if (_appParams.DAB)
            {
                var DABProcessor = new DABProcessor(logger);
                DABProcessor.ProcessingSubChannel = new DABSubChannel()
                {
                    StartAddr = 570,
                    Length = 72, // 90
                    Bitrate = 96,
                    ProtectionLevel =  EEPProtectionLevel.EEP_3
                };

                _demodulator = DABProcessor;
            }

            _demodulator.OnDemodulated += Program_OnDemodulated;
            _demodulator.OnFinished += Program_OnFinished;

            var bufferSize = 1024 * 1024;
            var IQDataBuffer = new byte[bufferSize];

            PowerCalculation powerCalculator = null;

            _demodStartTime = DateTime.Now;
            var lastBufferFillNotify = DateTime.MinValue;

            using (var inputFs = new FileStream(_appParams.InputFileName, FileMode.Open, FileAccess.Read))
            {
                logger.Info($"Total bytes : {inputFs.Length}");
                long totalBytesRead = 0;

                while (inputFs.Position <  inputFs.Length)
                {
                    var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);
                    totalBytesRead += bytesRead;

                    if ((DateTime.Now - lastBufferFillNotify).TotalMilliseconds > 500)
                    {
                        lastBufferFillNotify = DateTime.Now;
                        if (inputFs.Length > 0)
                        {
                            var percents = (totalBytesRead / (inputFs.Length / 100));
                            logger.Debug($" Processing input file:                   {percents} %");
                        }
                    }

                    if (powerCalculator == null)
                    {
                        powerCalculator = new PowerCalculation();
                        var power = powerCalculator.GetPowerPercent(IQDataBuffer, bytesRead);
                        logger.Info($"Power: {power.ToString("N0")} % dBm");
                    }

                    _demodulator.AddSamples(IQDataBuffer, bytesRead);

                    System.Threading.Thread.Sleep(200);
                }
            }

            _demodulator.Finish();

            while (!_fileProcessed)
            {
                System.Threading.Thread.Sleep(500);
            }
    }
  }

