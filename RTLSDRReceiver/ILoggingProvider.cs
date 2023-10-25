using LoggerService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public interface ILoggingProvider
    {
        ILoggingService GetLoggingService();
    }
}
