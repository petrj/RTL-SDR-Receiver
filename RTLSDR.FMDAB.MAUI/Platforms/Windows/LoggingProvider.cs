using RTLSDRReceiver;
using LoggerService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class LoggerProvider : ILoggingProvider
    {
        public ILoggingService GetLoggingService()
        {
            return new NLogLoggingService(Path.Join(AppContext.BaseDirectory, "Platforms\\Windows\\NLog.config"));
        }
    }
}
