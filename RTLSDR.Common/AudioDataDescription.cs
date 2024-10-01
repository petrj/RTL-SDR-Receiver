using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.Common
{
    public class AudioDataDescription
    {
        public int SampleRate { get; set; }
        public short Channels { get; set; }
        public short BitsPerSample { get; set; }        
    }
}
