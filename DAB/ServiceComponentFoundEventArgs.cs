using System;
namespace DAB
{
    public class ServiceComponentFoundEventArgs : EventArgs
    {
        public ServiceComponentDefinition ServiceComponent { get; set; }
    }
}
