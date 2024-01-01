using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LoggerService;

namespace DAB
{
    public class FIB
    {
        private ILoggingService _loggingService;

        public FIB(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public string EnsembleLabel { get; set; } = null;
        public string ServiceLabel { get; set; } = null;
        public string ServiceLabel32 { get; set; } = null;
        public int EnsembleIdentifier { get; set; } = -1;
        public int ServiceIdentifier { get; set; } = -1;
        public int ServiceIdentifier32 { get; set; } = -1;

        public string ServiceComponentLabel { get; set; } = null;
        public string ServiceComponentLabel32 { get; set; } = null;

        public static byte[] BitsByteArrayToByteArray(byte[] bitBytes, int offset = 0, int bytesCount = -1)
        {
            var res = new List<byte>();
            if (bytesCount == -1)
            {
                bytesCount = bitBytes.Length / 8;
            }

            for (var i = 0; i < bytesCount; i++)
            {
                byte b = 0;
                for (var j = 0; j < 8; j++)
                {
                    b += Convert.ToByte(bitBytes[offset+i * 8+j] << (7-j));
                }
                res.Add(b);
            }
            return res.ToArray();
        }

        public static void WriteBitsByteArrayToConsole(byte[] bytes, bool includeHexa = true)
        {
            WriteByteArrayToConsole(BitsByteArrayToByteArray(bytes), includeHexa);
        }

        public static void WriteByteArrayToConsole(byte[] bytes, bool includeHexa = true)
        {
            var sb = new StringBuilder();
            var sbc = new StringBuilder();
            var sbb = new StringBuilder();
            var sbh = new StringBuilder();
            int c = 0;
            int row = 0;

            for (var i = 0; i < bytes.Length; i++)
            {
                sbb.Append($"{Convert.ToString(bytes[i], 2).PadLeft(8, '0'),9} ");
                sbh.Append($"{("0x" + Convert.ToString(bytes[i], 16)).ToUpper().PadLeft(8, ' '),9} ");
                sb.Append($"{bytes[i].ToString(),9} ");


                if (bytes[i] >= 32 && bytes[i] <= 128)
                {
                    sbc.Append($"{Convert.ToChar(bytes[i]),9} ");
                }
                else
                {
                    sbc.Append($"{"",9} ");
                }
                c++;

                if (c >= 10)
                {
                    Console.WriteLine(sbb.ToString() + "  " + ((row + 1) * 10).ToString().PadLeft(3));
                    if (includeHexa)
                    {
                        Console.WriteLine(sbh.ToString());
                    }
                    Console.WriteLine(sb.ToString());
                    Console.WriteLine(sbc.ToString());
                    Console.WriteLine();
                    sb.Clear();
                    sbb.Clear();
                    sbc.Clear();
                    sbh.Clear();

                    c = 0;
                    row++;
                }
            }
            Console.WriteLine(sbb.ToString());
            if (includeHexa)
            {
                Console.WriteLine(sbh.ToString());
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine(sbc.ToString());
            Console.WriteLine();
        }

        public static bool getBoolBit(byte[] d, int offset)
        {
            return getBits(d, offset, 1) == 1;
        }

        public static byte[] getBitBytes(byte[] d, int offset, int size)
        {
            return BitsByteArrayToByteArray(d, offset, size / 8);
        }

        public static string getBitsASCIIString(byte[] d, int offset, int size)
        {
            if (offset + size > d.Length)
            {
                size = d.Length - offset;
            }
            var bytes = BitsByteArrayToByteArray(d, offset, size / 8);
            return Encoding.ASCII.GetString(bytes);
        }

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

        public static uint getBits(byte[] data, int offset)
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

        public static FIB Parse(byte[] data, int fib, ILoggingService loggingService)
        {
            var res = new FIB(loggingService);

            WriteBitsByteArrayToConsole(data);

            var processedBytes = 0;

            var dataPos = 0;

            while (processedBytes < 30)
            {
                var FIGtype = getBits(data, dataPos,3);
                switch (FIGtype)
                {
                    case 0:
                        res.ParseFIG0(data, dataPos);
                        break;

                    case 1:
                        res.ParseFIG1(data, dataPos);
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

        private void ParseFIG0(byte[] d, int dPosition = 0)
        {
            var headerType = getBits(d, dPosition, 3);
            var length = getBits(d, dPosition + 3, 5);
            var cn = FIB.getBoolBit(d, 8 + 0);
            var oe = FIB.getBoolBit(d, 8 + 1);
            var pd = FIB.getBoolBit(d, 8 + 2);
            var ext = getBits(d, dPosition, 8 + 3);
            // ....
        }

        private void ParseFIG1(byte[] d, int dPosition = 0)
        {
            var headerType = getBits(d, dPosition, 3);
            var length = getBits(d, dPosition + 3, 5);
            var charSet = getBits(d, dPosition + 8, 4);
            var cn = getBoolBit(d, dPosition + 8 + 4);
            if (cn)
            {
                return;
            }

            var extension = getBits(d, dPosition + 8 + 5, 3);

            switch (extension)
            {
                case 0: 
                    EnsembleIdentifier = Convert.ToInt32(FIB.getBits(d, dPosition + 16, 16));
                    EnsembleLabel = getBitsASCIIString(d, dPosition + 32, 16 * 8);
                    _loggingService.Info($"FIC: >>> Ensemble: Identifier: {EnsembleIdentifier}, label: {EnsembleLabel}");
                    break;

                case 1: // 16 bit Identifier field for service label
                    ServiceIdentifier = Convert.ToInt32(FIB.getBits(d, dPosition + 16, 16));
                    ServiceLabel = getBitsASCIIString(d, dPosition + 32, 16 * 8);
                    _loggingService.Info($"FIC: >>> Service: Identifier: {ServiceIdentifier}, label: {ServiceLabel}");
                    break;

                case 5: // 32 bit Identifier field for service label
                    ServiceIdentifier32 = Convert.ToInt32(FIB.getBits(d, dPosition + 16, 32));
                    ServiceLabel32 = getBitsASCIIString(d, dPosition + 16 + 32, 16 * 8);
                    _loggingService.Info($"FIC: >>> Service32: Identifier: {ServiceIdentifier32}, label: {ServiceLabel32}");
                    break;

                case 4: // Service Component Label

                    var pd = getBoolBit(d, dPosition + 16);
                    var SCIdS = getBits(d, dPosition + 16 + 4, 4);
                    if (pd)
                    {
                        ServiceIdentifier32 = Convert.ToInt32(FIB.getBits(d, dPosition + 16 + 8, 32));
                        ServiceComponentLabel32 = getBitsASCIIString(d, dPosition + 16 + 8 + 32, 16 * 8);

                        _loggingService.Info($"FIC: >>> Service Component Label32: Identifier: {ServiceIdentifier32}, label: {ServiceComponentLabel32}");
                    } else
                    {
                        ServiceIdentifier = Convert.ToInt32(FIB.getBits(d, dPosition + 16 + 8, 16));
                        ServiceComponentLabel = getBitsASCIIString(d, dPosition + 16 + 8 + 16, 16 * 8);

                        _loggingService.Info($"FIC: >>> Service Component Label: Identifier: {ServiceIdentifier}, label: {ServiceComponentLabel}");
                    }

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
                /*
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
                    }
                    break;
                    */
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
