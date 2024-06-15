using LoggerService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace RTLSDR
{
    public class RTLSRDTestDriver : ISDR
    {
        private ILoggingService _loggingService;

        public RTLSRDTestDriver(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        private int _frequency = 104000000;
        private double _RTLBitrate = 0;

        public string DeviceName
        {
            get
            {
                return "Test driver";
            }
        }

        public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

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
                return Convert.ToInt32(_RTLBitrate);
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

        }

        public void Init(DriverInitializationResult driverInitializationResult)
        {
            new Thread(() =>
            {
                _loggingService.Info($"RTLSRDTestDriver thread started");

                var bufferSize = 1024 * 1024;
                var IQDataBuffer = new byte[bufferSize];
                var lastBufferFillNotify = DateTime.MinValue;

                using (var inputFs = new FileStream("c:\\temp\\FM.raw", FileMode.Open, FileAccess.Read))
                {
                    _loggingService.Info($"Total bytes : {inputFs.Length}");
                    long totalBytesRead = 0;

                    while (inputFs.Position < inputFs.Length)
                    {
                        var bytesRead = inputFs.Read(IQDataBuffer, 0, bufferSize);
                        totalBytesRead += bytesRead;

                        if (OnDataReceived != null)
                        {
                            //_powerPercent = Demodulator.PercentSignalPower;

                            OnDataReceived(this, new OnDataReceivedEventArgs()
                            {
                                Data = IQDataBuffer,
                                Size = bytesRead
                            });
                        }

                        if ((DateTime.Now - lastBufferFillNotify).TotalMilliseconds > 1000)
                        {
                            lastBufferFillNotify = DateTime.Now;
                            if (inputFs.Length > 0)
                            {
                                var percents = totalBytesRead / (inputFs.Length / 100);
                                _loggingService.Debug($" Processing input file:                   {percents} %");
                            }
                        }

                        System.Threading.Thread.Sleep(125);
                    }
                }

            }).Start();
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

        }

        public void SetFrequencyCorrection(int correction)
        {

        }

        public void SetGain(int gain)
        {

        }

        public void SetGainMode(bool manual)
        {

        }

        public void SetIfGain(bool ifGain)
        {

        }

        public void SetSampleRate(int sampleRate)
        {

        }
    }
}
