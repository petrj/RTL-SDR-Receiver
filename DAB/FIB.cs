using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DAB
{
    public class FIB
    {
        public static uint getBits(byte[] d, int offset, uint size)
        {
            if (size > 32)
            {
                throw new Exception("getBits called with size>32");
            }

            var res = 0;

            for (int i = 0; i < size; i++)
            {
                res <<= 1;
                res |= d[offset + i];
            }

            return Convert.ToUInt32(res);
        }

        public static uint getBits_3(byte[] data, int offset)
        {
            var res = data[offset];
            res <<= 1;
            res |= data[offset + 1];
            res <<= 1;
            res |= data[offset + 2];
            return res;
        }

        public static uint getBits_5(byte[] data, int offset)
        {
            var res = data[offset];
            res <<= 1;
            res |= data[offset + 1];
            res <<= 1;
            res |= data[offset + 2];
            res <<= 1;
            res |= data[offset + 3];
            res <<= 1;
            res |= data[offset + 4];
            return res;
        }

        public static uint getBits_6(byte[] d, int offset)
        {
            var res = d[offset];
            res <<= 1;
            res |= d[offset + 1];
            res <<= 1;
            res |= d[offset + 2];
            res <<= 1;
            res |= d[offset + 3];
            res <<= 1;
            res |= d[offset + 4];
            res <<= 1;
            res |= d[offset + 5];
            return res;
        }

        public static uint getBits_7(byte[] d, int offset)
        {
            var res = d[offset];
            res <<= 1;
            res |= d[offset + 1];
            res <<= 1;
            res |= d[offset + 2];
            res <<= 1;
            res |= d[offset + 3];
            res <<= 1;
            res |= d[offset + 4];
            res <<= 1;
            res |= d[offset + 5];
            res <<= 1;
            res |= d[offset + 6];
            return res;
        }

        public static byte getBits_8(byte[] d, int offset)
        {
            var res = d[offset];
            res <<= 1;
            res |= d[offset + 1];
            res <<= 1;
            res |= d[offset + 2];
            res <<= 1;
            res |= d[offset + 3];
            res <<= 1;
            res |= d[offset + 4];
            res <<= 1;
            res |= d[offset + 5];
            res <<= 1;
            res |= d[offset + 6];
            res <<= 1;
            res |= d[offset + 7];
            return res;
        }

        public static uint getBits_1(byte[] d, int offset)
        {
            return (Convert.ToByte(d[offset] & 0x01));
        }

        public static uint getBits_2(byte[] d, int offset)
        {
            var res = d[offset];
            res <<= 1;
            res |= d[offset + 1];
            return res;
        }

        public static uint getBits_4(byte[] d, int offset)
        {
            var res = d[offset];
            res <<= 1;
            res |= d[offset + 1];
            res <<= 1;
            res |= d[offset + 2];
            res <<= 1;
            res |= d[offset + 3];
            return res;
        }

        public static FIB Parse(byte[] data, int fib)
        {
            var res = new FIB();

            var processedBytes = 0;

            var dataPos = 0;

            while (processedBytes < 30)
            {
                var FIGtype = getBits_3(data, dataPos);
                switch (FIGtype)
                {
                    case 0:
                        //process_FIG0(data);
                        break;

                    case 1:
                        res.ParseFIG1(data);
                        break;

                    case 2:
                        //process_FIG2(data);
                        break;

                    case 7:
                        return null;

                    default:
                        //std::clog << "FIG%d present" << FIGtype << std::endl;
                        break;
                }
                processedBytes += Convert.ToInt32(getBits_5(data, dataPos + 3) + 1);
                dataPos += processedBytes * 8;
            }

            return res;
        }

        private void ParseFIG1(byte[] d, int dPosition = 0)
        {
            uint SId = 0;
            int offset = 0;
            uint pd_flag;
            uint SCidS;
            string label = null;

            // FIG 1 first byte
            var charSet = FIB.getBits_4(d, 8 + dPosition);
            var oe = FIB.getBits_1(d, 8 + 4 + dPosition);
            var extension = FIB.getBits_3(d, 8 + 5 + dPosition);
            //label[16] = 0x00;
            if (oe == 1)
            {
                return;
            }

            switch (extension)
            {
                case 0: // ensemble label
                    {
                        var EId = FIB.getBits(d, 16 + dPosition, 16);  // ensembleId
                        offset = 32;

                        var labelBytes = new byte[16];
                        for (int i = 0; i < 16; i++)
                        {
                            labelBytes[i] = FIB.getBits_8(d, offset + dPosition);
                            offset += 8;
                        }

                        label = ASCIIEncoding.ASCII.GetString(labelBytes);

                        break;
                    }

                case 1: // 16 bit Identifier field for service label
                    SId = FIB.getBits(d, 16+dPosition, 16); // ServiceId
                    offset = 32;
                    break;

                /*
                case 3: // Region label
                    //uint8_t region_id = getBits_6 (d, 16 + 2);
                    offset = 24;
                    for (int i = 0; i < 16; i ++) {
                        label[i] = getBits_8 (d, offset + 8 * i);
                    }

                    //        std::clog << "fib-processor:" << "FIG1/3: RegionID = %2x\t%s\n", region_id, label) << std::endl;
                    break;
                */

                case 4: // Component label
                    pd_flag = FIB.getBits(d, 16 + dPosition, 1);
                    SCidS = FIB.getBits(d, 20 + dPosition, 4);
                    if (pd_flag == 1)
                    {  // 32 bit identifier field for service component label
                        SId = FIB.getBits(d, 24 + dPosition, 32);
                        offset = 56;
                    }
                    else
                    {  // 16 bit identifier field for service component label
                        SId = FIB.getBits(d, 24 + dPosition, 16);
                        offset = 40;
                    }

                    for (int i = 0; i < 16; i++)
                    {
                        var chrByte = FIB.getBits_8(d, offset + dPosition);
                        label += ASCIIEncoding.ASCII.GetString(new byte[] { chrByte });
                        offset += 8;
                    }

                    break;


                case 5: // 32 bit Identifier field for service label
                    SId = FIB.getBits(d, 16 + dPosition, 32);
                    offset = 48;
                    /*service = findServiceId(SId);
                    if (service)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            label[i] = getBits_8(d, offset);
                            offset += 8;
                        }
                        service->serviceLabel.fig1_flag = getBits(d, offset, 16);
                        service->serviceLabel.fig1_label = label;
                        service->serviceLabel.setCharset(charSet);
                    }*/
                    break;

                /*
                case 6: // XPAD label
                */

                default:
                    // std::clog << "fib-processor:" << "FIG1/%d: not handled now\n", extension) << std::endl;
                    break;
            }
        }
    }
}
