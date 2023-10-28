using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR
{
    public class DriverSettings
    {
        public int Port { get; set; } = 1234;
        public int SampleRate { get; set; } = 2048000;
        public string IP { get; set; } = "127.0.0.1";
    }
}
