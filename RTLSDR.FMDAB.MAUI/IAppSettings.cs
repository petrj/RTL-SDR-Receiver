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

        int FMFrequencyKHz { get; set; }
        int FMDriverSampleRate { get; set; }
        int FMAudioSampleRate { get; set; }
        bool FMDeEmphasis { get; set; }
        bool FMFastArcTan { get; set; }

        int DABFrequencyKHz { get; set; }
        int DABDriverSampleRate { get; set; }

        ModeEnum Mode { get; set; }
    }
}

