using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class RTLSDRDriverSettings
    {
        public int Port { get; set; } = 1234;
        public int SampleRate { get; set; } = 2048000;
    }
}
