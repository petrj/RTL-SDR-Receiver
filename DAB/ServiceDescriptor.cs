using System;
namespace DAB
{
    public class ServiceDescriptor
    {
        public string ServiceLabel { get; set; } = null;
        //public string ServiceLabel32 { get; set; } = null;
        public int ServiceIdentifier { get; set; } = -1;
        //public int ServiceIdentifier32 { get; set; } = -1;

        public override string ToString()
        {
            return $"{ServiceLabel} (id {ServiceIdentifier})";
        }
    }
}
