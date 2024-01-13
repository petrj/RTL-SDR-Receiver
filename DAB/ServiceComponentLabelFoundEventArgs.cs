using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class ServiceComponentLabelFoundEventArgs : EventArgs
    {
        public ServiceComponentLabel ServiceLabel  { get;set; }
    }
}
