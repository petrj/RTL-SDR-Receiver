using LoggerService;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RTLSDR.Common
{
    public class ThreadWorker
    {
        //private ConcurrentQueue<object> _queue;
        private Thread _thread = null;
        private string _name;
        private ILoggingService _logger = null;

        private int _actionMSDelay = 1000;

        private const int MinThreadNoDataMSDelay = 25;

        private Action<Array[]> _action = null;

        private bool _running = false;

        public ThreadWorker(ILoggingService logger, string name = "Threadworker")
        {
            _logger = logger;
            _name = name;
            _logger.Debug($"Starting Threadworker {name}");
        }

        public void SetThreadMethod(Action<Array[]> action, int actionMSDelay)
        {
            _action = action;
            _actionMSDelay = actionMSDelay;
        }

        public void Start()
        {
            _logger.Debug($"Threadworker {_name} starting");

            _running = true;
            _thread = new Thread(ThreadLoop);
            _thread.Start();
        }

        public void Stop()
        {
            _logger.Debug($"Stopping Threadworker {_name}");
            _running = false;
        }

        private void ThreadLoop()
        {
            while (_running)
            {
                if (_action  != null)
                {
                    _action(new Array[] { });
                }
                Thread.Sleep(_actionMSDelay);
            }

            _logger.Debug($"Threadworker {_name} stopped");
        }        
    }
}
