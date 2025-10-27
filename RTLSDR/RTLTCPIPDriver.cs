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

    public class RTLTCPIPDriver : RTLSDRDriver
    {
        public RTLTCPIPDriver(ILoggingService loggingService)
            : base(loggingService)
        {
        }

        private Process _process;

        protected override async Task Connect()
        {
            Task.Run(() =>
            {
                Run("rtl_tcp", $"-f {Frequency} -s {Settings.SDRSampleRate} -g {Settings.Gain}", _loggingService);
            });

            _loggingService.Info("Waiting 5 secs for init driver");
            await Task.Delay(5000);

            await base.Connect();
        }

        public override void Disconnect()
        {
            base.Disconnect();

            _process?.Kill(true);
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
    }

}