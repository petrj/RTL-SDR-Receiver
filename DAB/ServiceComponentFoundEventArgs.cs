using System;
namespace DAB
{
    public class ServiceComponentFoundEventArgs : EventArgs
    {
        public DABService ServiceComponent { get; set; }
    }
}
