using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class SubChannelDescriptor
    {
        public uint SubChId { get; set; }
        public uint StartAddr { get; set; }
        public uint Length { get; set; }
    }
}
