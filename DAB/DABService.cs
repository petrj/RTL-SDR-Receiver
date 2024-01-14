using System;
using System.Collections.Generic;
using System.Text;

namespace DAB
{
    public class DABService
    {
        public string ServiceName { get; set; } = null; // filled from Service component global definition

        public uint ServiceNumber { get; set; } // Service reference
        public string CountryId { get; set; }
        public string ExtendedCountryCode { get; set; } // ECC

        public int ServiceIdentifier { get; set; } = -1; // filled from Service component global definition

        public List<DABComponent> Components { get; set; }

        public DABService()
        {
            Components = new List<DABComponent>();
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

            res.AppendLine($"\t----Service component-----------------");
            res.AppendLine($"\tServiceName:             {ServiceName}");
            res.AppendLine($"\tServiceNumber:           {ServiceNumber}");
            res.AppendLine($"\tServiceIDentifier:       {ServiceIdentifier}");
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
                    res.AppendLine($"\t#{i.ToString().PadLeft(5,' ')}:    SubChId :     {a.SubChId} (pr: {a.Primary})");
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
