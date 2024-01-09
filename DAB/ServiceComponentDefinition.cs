using System;
using System.Collections.Generic;

namespace DAB
{
    public class ServiceComponentDefinition
    {
        public int ServiceId { get; set; }
        public string CountryId { get; set; }
        public uint ServiceNumber { get; set; } // Service reference
        public string ExtendedCountryCode { get; set; } // ECC

        public List<ServiceComponentDescription> Components { get; set; }

        public ServiceComponentDefinition()
        {
            Components = new List<ServiceComponentDescription>();
        }
    }
}
