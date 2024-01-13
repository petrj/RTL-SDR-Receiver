using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class ServiceComponentGlobalDefinition
    {
        public uint ServiceIdentifier { get; set; }
        public uint SCIdS { get; set; }
        public uint SCId { get; set; }
        public uint SubChId { get; set; }

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Service component global definition-----------------");
            res.AppendLine($"\tServiceIdentifier:           {ServiceIdentifier}");
            res.AppendLine($"\tSCIdS:                       {SCIdS}");
            res.AppendLine($"\tSCId:                        {SCId}");
            res.AppendLine($"\tSubChId:                     {SubChId}");
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
