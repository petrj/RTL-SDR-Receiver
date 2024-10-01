using LoggerService;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;


namespace RTLSDR
{

    public class RTLTCPIPDriver : ISDR
    {
        private ILoggingService _loggingService;

        public RTLTCPIPDriver(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        private int _frequency = 104000;

        private double _bitrate = 0;

         public DriverStateEnum State { get; private set; } = DriverStateEnum.NotInitialized;

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

            State = DriverStateEnum.DisConnected;
        }

        public void SetErrorState()
        {
            _loggingService.Info($"Setting manually error state");
            State = DriverStateEnum.Error;
        }

        private void RunCommand(string command, string args)
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();            


            p.OutputDataReceived += (sender, a) => 
            {
                _loggingService.Info($"received output: {a.Data}");
            };

            p.StartInfo.FileName = command;
            //p.StartInfo.UseShellExecute = false;
            p.StartInfo.Arguments = args;
            //p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            //p.StartInfo.RedirectStandardError = true;              
            //p.EnableRaisingEvents = true;
            p.Start();
            p.BeginOutputReadLine();
            p.WaitForExit();
            p.CancelOutputRead();
        }

        public void Init(DriverInitializationResult driverInitializationResult)
        {
            _loggingService.Info($"Starting {DeviceName}");

            RunCommand("rtl_tcp","-f 104000");

            _loggingService.Info($"{DeviceName} stopped");
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
            _frequency = freq;
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