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

        public RTLSDRCommand(RTLSDRCommandsEnum command, int intArgument)
        {
            Command = command;

            byte[] arguments = new byte[4];

            arguments[0] = (byte)((intArgument >> 24) & 0xff);
            arguments[1] = (byte)((intArgument >> 16) & 0xff);
            arguments[2] = (byte)((intArgument >> 8) & 0xff);
            arguments[3] = (byte)(intArgument & 0xff);

            Arguments = arguments;
        }

        public RTLSDRCommand(RTLSDRCommandsEnum command, short arg1, short arg2)
        {
            Command = command;

            byte[] arguments = new byte[4];

            arguments[0] = (byte)((arg1 >> 8) & 0xff);
            arguments[1] = (byte)(arg1 & 0xff);
            arguments[2] = (byte)((arg2 >> 8) & 0xff);
            arguments[3] = (byte)(arg2 & 0xff);

            Arguments = arguments;
        }

        public byte[] ToByteArray()
        {
            var res = new List<byte>();
            res.Add((byte)Command);

            var arArray = new byte[4];
            for (var i=0; i < 4; i++)
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
