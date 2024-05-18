using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public interface IAppSettings
    {
        int FrequencyKHz { get; set; }
        ModeEnum Mode { get; set; }
    }
}
