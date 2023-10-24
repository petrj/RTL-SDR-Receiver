using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DABReceiver
{
    public class DABDriverSettings
    {
        public int Port { get; set; } = 1234;
        public int SampleRate { get; set; } = 2048000;
    }
}
