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
    public class ThreadWorker<T>
    {
        private ConcurrentQueue<T> _queue;
        private Thread _thread = null;
        private string _name;
        private ILoggingService _logger = null;

        private int _actionMSDelay = 1000;

        private DateTime _timeStarted = DateTime.MinValue;

        private const int MinThreadNoDataMSDelay = 25;

        private Action<T> _action = null;

        private bool _running = false;

        public bool ReadingQueue { get; set; } = false;

        public ThreadWorker(ILoggingService logger, string name = "Threadworker")
        {
            _logger = logger;
            _name = name;
            _logger.Debug($"Starting Threadworker {name}");
        }

        public void SetThreadMethod(Action<T> action, int actionMSDelay)
        {
            _action = action;
            _actionMSDelay = actionMSDelay;
        }

        public void SetQueue(ConcurrentQueue<T> queue)
        {
            _queue = queue;
        }

        public void Start()
        {
            _logger.Debug($"Threadworker {_name} starting");
            _timeStarted = DateTime.Now;

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
                var data = default(T);

                if (ReadingQueue &&  (_queue != null))
                {
                    _queue.TryDequeue(out data);
                }

                if (_action != null)
                {
                    _action(data);
                }
                Thread.Sleep(_actionMSDelay);
            }

            _logger.Debug($"Threadworker {_name} stopped");
        }        
    }
}
