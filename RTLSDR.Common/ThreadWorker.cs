using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RTLSDR.Common
{
    public class ThreadWorker
    {
        //private ConcurrentQueue<object> _queue;

        private const int MinThreadNoDataMSDelay = 25;

        private bool _running = false;

        public ThreadWorker()
        {

        }

        public void StartActionInThread(Action action)
        {
            _running = true;

            Task.Factory.StartNew(() =>
            {
                // Whatever code you want in your thread
                while (_running)
                {
                    Console.WriteLine("Worker thread running ....");
                    Thread.Sleep(1000);
                }
            }).;

            Console.WriteLine("Worker thread stopped");
        }
    }
}
