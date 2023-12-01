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
    }
}
