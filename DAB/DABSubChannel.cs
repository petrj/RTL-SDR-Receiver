using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class DABSubChannel
    {
        public uint SubChId { get; set; }
        public uint StartAddr { get; set; }
        public uint Length { get; set; }

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Sub channel-----------------");
            res.AppendLine($"\tSubChId:           {SubChId}");
            res.AppendLine($"\tStartAddr:         {StartAddr}");
            res.AppendLine($"\tLength:            {Length}");
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
