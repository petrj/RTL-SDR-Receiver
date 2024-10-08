﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class DABSubChannel
    {
        public uint SubChId { get; set; }
        public uint StartAddr { get; set; }
        public uint Length { get; set; }
        public int Bitrate { get; set; }
        public EEPProtectionLevel ProtectionLevel { get; set; } = EEPProtectionLevel.EEP_1;

        public EEPProtectionProfile ProtectionProfile { get; set; } = EEPProtectionProfile.EEP_A;


        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\t----Sub channel-----------------");
            res.AppendLine($"\tSubChId:           {SubChId}");
            res.AppendLine($"\tStartAddr:         {StartAddr}");
            res.AppendLine($"\tLength:            {Length}");
            res.AppendLine($"\tBitrate:           {Bitrate}");
            res.AppendLine($"\tEEP:               {ProtectionLevel}");
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
