using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class ServiceComponentLabel
    {
        public uint ServiceIdentifier { get; set; }

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Service component label-----------------");
            res.AppendLine($"\tServiceIdentifier:           {ServiceIdentifier}");
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
