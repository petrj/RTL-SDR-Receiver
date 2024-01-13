using System;
using System.Text;

namespace DAB
{
    public class DABProgrammeServiceLabel
    {
        public string ServiceLabel { get; set; } = null;
        public int ServiceIdentifier { get; set; } = -1;

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Service-----------------------------");
            res.AppendLine($"\tServiceLabel:           {ServiceLabel}");
            res.AppendLine($"\tServiceIdentifier:      {ServiceIdentifier}");
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
