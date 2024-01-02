using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class ServiceFoundEventArgs : EventArgs
    {
        public ServiceDescriptor Service { get; set; }
    }
}
