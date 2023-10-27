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

        public byte[] ToByteArray()
        {
            var res = new List<byte>();
            res.Add((byte)Command);

            var arArray = new byte[4];
            for (var i=0; i < 4;)
            {
                arArray[i] = Arguments == null || Arguments.Length > i
                    ? (byte)0
                    : Arguments[i];
            }

            res.AddRange(arArray);

            return res.ToArray();
        }
    }
}
