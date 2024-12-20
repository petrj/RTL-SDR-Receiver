﻿using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABService : IAudioService
    {
        public string ServiceName { get; set; } = null; // filled from Service component global definition
        public uint ServiceNumber { get; set; } // Service reference

        public string CountryId { get; set; }
        public string ExtendedCountryCode { get; set; } // ECC

        public List<DABComponent> Components { get; set; }

        public DABService()
        {
            Components = new List<DABComponent>();
        }

        public int SubChannelsCount
        {
            get
            {
                if (Components == null)
                    return 0;

                return Components.Count;
            }
        }

        public DABSubChannel FirstSubChannel
        {
            get
            {
                if ((Components == null) ||
                    (Components.Count == 0) ||
                    (Components[0].SubChannel == null)
                    )
                {
                    return null;
                }

                return Components[0].SubChannel;
            }
        }


        public void SetServiceIdentifier(DABServiceComponentGlobalDefinition definition)
        {
            /*
            if (ServiceIdentifier == -1)
            {
                foreach (var component in Components)
                {
                    if (component != null &&
                        component.SubChannel.SubChId == definition.SubChId)
                    {
                        ServiceIdentifier = Convert.ToInt32(definition.ServiceIdentifier);
                        break;
                    }
                }
            }
            */
        }

        public void SetSubChannels(Dictionary<uint,DABSubChannel> SubChanels)
        {
            foreach (var component in Components)
            {
                foreach (var subc in SubChanels)
                {
                    if (component.SubChannel == null &&
                        component.Description is MSCStreamAudioDescription a &&
                        subc.Key == a.SubChId)
                    {
                        component.SubChannel = subc.Value;
                    }
                }
            }
        }

        public void SetServiceLabels(Dictionary<uint, DABProgrammeServiceLabel> ServiceLabels)
        {
            if (ServiceName == null)
            {
                foreach (var label in ServiceLabels)
                {
                    if (label.Key == ServiceNumber && ServiceName == null)
                    {
                        ServiceName = label.Value.ServiceLabel;
                    }
                }
            }
        }

        public DABComponent GetComponentBySubChId(uint subChId)
        {
            foreach (var component in Components)
            {
                if (component.Description is MSCStreamAudioDescription ad)
                {
                    if (ad.SubChId == subChId)
                    {
                        return component;
                    }
                }
                if (component.Description is MSCStreamDataDescription dd)
                {
                    if (dd.SubChId == subChId)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        public override string ToString()
        {
            var res = new StringBuilder();

            res.AppendLine($"\tServiceName:             {ServiceName}");
            res.AppendLine($"\tServiceNumber:           {ServiceNumber}");
            res.AppendLine($"\tCountryId:               {CountryId}");
            res.AppendLine($"\tExtendedCountryCode:     {ExtendedCountryCode}");
            res.AppendLine($"\tComponentsCount:         {Components.Count}");

            for (var i=0;i< Components.Count;i++)
            {
                if (Components[i].SubChannel == null)
                {
                    res.AppendLine($"\t                         No sub channel yet");
                } else
                {
                    res.AppendLine($"\tSubchannel:");
                    res.AppendLine($"\t  StartAddr:             {Components[i].SubChannel.StartAddr}");
                    res.AppendLine($"\t  Length   :             {Components[i].SubChannel.Length}");
                }

                if (Components[i].Description is MSCStreamAudioDescription a)
                {
                    res.AppendLine($"\t#{i.ToString().PadLeft(5,' ')}:    SubChId:     {a.SubChId} (pr: {a.Primary})");

                    if (Components[i].SubChannel != null)
                    {
                        res.AppendLine($"\t           BitRate:     {Components[i].SubChannel.Bitrate}");
                        res.AppendLine($"\t           EEP    :     {Components[i].SubChannel.ProtectionLevel}");
                    }
                    res.AppendLine($"\t           Audio");
                }
                if (Components[i].Description is MSCStreamDataDescription d)
                {
                    res.AppendLine($"\t#{i.ToString().PadLeft(5,' ')}:    SubChId :     {d.SubChId} (pr: {d.Primary})");
                    res.AppendLine($"\t           Data");
                }
                if (Components[i].Description is MSCPacketDataDescription p)
                {
                    res.AppendLine($"\t#{i.ToString().PadLeft(5, ' ')}:    Identifier:      {p.ServiceComponentIdentifier} (pr: {p.Primary})");
                    res.AppendLine($"\t           Packets");
                }
            }
            res.AppendLine($"\t----------------------------------------");

            return res.ToString();
        }
    }
}
