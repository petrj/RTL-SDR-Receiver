using System;
using System.Collections.Generic;
using System.Text;

namespace DAB
{
    public class ServiceComponentDefinition
    {
        public string CountryId { get; set; }
        public uint ServiceNumber { get; set; } // Service reference
        public string ExtendedCountryCode { get; set; } // ECC

        public List<MSCDescription> Components { get; set; }

        public ServiceComponentDefinition()
        {
            Components = new List<MSCDescription>();
        }

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Service component-----------------");
            res.AppendLine($"\tServiceNumber:           {ServiceNumber}");
            res.AppendLine($"\tCountryId:               {CountryId}");
            res.AppendLine($"\tExtendedCountryCode:     {ExtendedCountryCode}");
            res.AppendLine($"\tComponentsCount:         {Components.Count}");

            for (var i=0;i< Components.Count;i++)
            {
                if (Components[i] is MSCStreamAudioDescription a)
                {
                    res.AppendLine($"\t#{i}:    SubChId :         {a.SubChId} (pr: {a.Primary})");
                }
            }
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
