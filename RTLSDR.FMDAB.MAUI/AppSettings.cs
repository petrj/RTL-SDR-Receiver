using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class AppSettings : IAppSettings
    {
        public int FrequencyKHz
        {
            get
            {
                return Preferences.Default.Get<int>("FrequencyKHz", 104000);
            }
            set
            {
                Preferences.Default.Set<int>("FrequencyKHz", value);
            }
        }

        public int FMDriverSampleRate
        {
            get
            {
                return Preferences.Default.Get<int>("FMDriverSampleRate", 1056000);
            }
            set
            {
                Preferences.Default.Set<int>("FMDriverSampleRate", value);
            }
        }

        public bool FMFastArcTan
        {
            get
            {
                return Preferences.Default.Get<bool>("FMFastArcTan", true);
            }
            set
            {
                Preferences.Default.Set<bool>("FMFastArcTan", value);
            }
        }

        public bool FMDeEmphasis
        {
            get
            {
                return Preferences.Default.Get<bool>("FMDeEmphasis", false);
            }
            set
            {
                Preferences.Default.Set<bool>("FMDeEmphasis", value);
            }
        }

        public int FMAudioSampleRate
        {
            get
            {
                return Preferences.Default.Get<int>("FMAudioSampleRate", 96000);
            }
            set
            {
                Preferences.Default.Set<int>("FMAudioSampleRate", value);
            }
        }

        public int DABDriverSampleRate
        {
            get
            {
                return Preferences.Default.Get<int>("DABDriverSampleRate", 2048000);
            }
            set
            {
                Preferences.Default.Set<int>("DABDriverSampleRate", value);
            }
        }

        public int DABFrequencyKHz
        {
            get
            {
                return Preferences.Default.Get<int>("DABFrequencyKHz", 192352); // 7C
            }
            set
            {
                Preferences.Default.Set<int>("DABFrequencyKHz", value);
            }
        }

        public int FMFrequencyKHz
        {
            get
            {
                return Preferences.Default.Get<int>("FMFrequencyKHz", 104000);
            }
            set
            {
                Preferences.Default.Set<int>("FMFrequencyKHz", value);
            }
        }

        public ModeEnum Mode
        {
            get
            {
                var mode = Preferences.Default.Get<int>("Mode", (int)ModeEnum.FM);
                return (ModeEnum)mode;
            }
            set
            {
                Preferences.Default.Set<int>("Mode", (int)value);
            }
        }
    }
}
