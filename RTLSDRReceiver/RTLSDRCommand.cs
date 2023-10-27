using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class RTLSDRCommand
    {
        public RTLSDRCommandsEnum Command { get; set; }
        public byte[] Arguments { get; set; }

        public RTLSDRCommand(RTLSDRCommandsEnum command, byte[] arguments)
        {
            Command = command;
            Arguments = arguments;
        }
    }
}
