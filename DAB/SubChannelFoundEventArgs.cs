using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class SubChannelFoundEventArgs : EventArgs
    {
        public DABSubChannel SubChannel { get; set; }
    }
}
