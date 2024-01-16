using System;
using System.Text;

namespace DAB
{
    public class DABProgrammeServiceLabel
    {
        public string ServiceLabel { get; set; } = null;

        public uint ServiceNumber { get; set; } // Service reference
        public string CountryId { get; set; }
        public string ExtendedCountryCode { get; set; } // ECC

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Service-----------------------------");
            res.AppendLine($"\tServiceLabel:           {ServiceLabel}");
            res.AppendLine($"\tServiceIdentifier:      {ServiceNumber}");
            res.AppendLine($"\tCountryId:              {CountryId}");
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
