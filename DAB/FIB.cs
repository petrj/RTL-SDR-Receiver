using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LoggerService;

namespace DAB
{
    /*
    Free .NET DAB+ library

    -   based upon welle.io (https://github.com/AlbrechtL/welle.io)
    -   DAB documentation: https://www.etsi.org/deliver/etsi_en/300400_300499/300401/02.01.01_60/en_300401v020101p.pdf
    */

    /// <summary>
    /// FIB - Fast Information Block
    /// </summary>
    public class FIB
    {
        private ILoggingService _loggingService;

        public FIB(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event EventHandler ServiceFound;
        public event EventHandler EnsembleFound;
        public delegate void ServiceFoundEventHandler(object sender, ServiceFoundEventArgs e);
        public delegate void EnsembleFoundEventHandler(object sender, EnsembleFoundEventArgs e);

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

        public static bool GetBitsBool(byte[] d, int offset)
        {
            return GetBitsNumber(d, offset, 1) == 1;
        }

        public static byte[] GetBitBytes(byte[] d, int offset, int size)
        {
            return BitsByteArrayToByteArray(d, offset, size / 8);
        }

        public static uint GetBitsNumber(byte[] d, int offset, uint size)
        {
            if (size > 32)
            {
                throw new Exception("GetBitsNumber: size>32");
            }

            var res = 0;

            for (int i = 0; i < size; i++)
            {
                res <<= 1;
                res |= d[offset + i];
            }

            return Convert.ToUInt32(res);
        }

        public void Parse(byte[] data, int fib)
        {
            int processedBytes = 0;
            var dataPos = 0;

            while (processedBytes < 30 && dataPos< 30 * 8)
            {
                try
                {
                    var FIGtype = GetBitsNumber(data, dataPos, 3);
                    var FIGLength = GetBitsNumber(data, dataPos + 3, 5) + 1;
                    switch (FIGtype)
                    {
                        case 0:
                            ParseFIG0(data, dataPos);
                            break;

                        case 1:
                            ParseFIG1(data, dataPos);
                            break;

                        case 2:
                            //process_FIG2(data);
                            break;

                        default:
                            break;
                    }
                    processedBytes += Convert.ToInt32(FIGLength);
                    dataPos += processedBytes * 8;
                }
                catch (Exception ex)
                {
                    _loggingService.Error(ex);
                    break;

                    // TODO: look to next FIG
                }
            }
        }

        private void ParseFIG0(byte[] d, int dPosition = 0)
        {
            var headerType = GetBitsNumber(d, dPosition, 3);
            var length = GetBitsNumber(d, dPosition + 3, 5);
            var cn = FIB.GetBitsBool(d, 8 + 0);
            var oe = FIB.GetBitsBool(d, 8 + 1);
            var pd = FIB.GetBitsBool(d, 8 + 2);
            var ext = GetBitsNumber(d, dPosition, 8 + 3);
            // ....
        }

        public void ParseFIG1(byte[] d, int dPosition = 0)
        {
            var headerType = GetBitsNumber(d, dPosition, 3);
            var length = GetBitsNumber(d, dPosition + 3, 5);
            var charSet = GetBitsNumber(d, dPosition + 8, 4);
            var cn = GetBitsBool(d, dPosition + 8 + 4);
            if (cn)
            {
                return;
            }
            var extension = GetBitsNumber(d, dPosition + 8 + 5, 3);

            switch (extension)
            {
                case 0:

                    if (EnsembleFound != null)
                    {
                        EnsembleFound(this, new EnsembleFoundEventArgs()
                        {
                            Ensemble = new EnsembleDescriptor()
                            {
                                EnsembleIdentifier = Convert.ToInt32(FIB.GetBitsNumber(d, dPosition + 16, 16)),
                                EnsembleLabel = EBUEncoding.GetString(GetBitBytes(d, dPosition + 32, 16 * 8))
                            }
                        });
                    }
                    return;

                case 1: // 16 bit Identifier field for service label

                    if (ServiceFound != null)
                    {
                        ServiceFound(this, new ServiceFoundEventArgs()
                        {
                             Service = new ServiceDescriptor()
                             {
                                 ServiceIdentifier = Convert.ToInt32(FIB.GetBitsNumber(d, dPosition + 16, 16)),
                                 ServiceLabel = EBUEncoding.GetString(GetBitBytes(d, dPosition + 32, 16 * 8))
                             }
                        });
                    }

                    return;

                case 5: // 32 bit Identifier field for service label

                    if (ServiceFound != null)
                    {
                        ServiceFound(this, new ServiceFoundEventArgs()
                        {
                            Service = new ServiceDescriptor()
                            {
                                ServiceIdentifier = Convert.ToInt32(FIB.GetBitsNumber(d, dPosition + 16, 32)),
                                ServiceLabel = EBUEncoding.GetString(GetBitBytes(d, dPosition + 16 + 32, 16 * 8))
                            }
                        });
                    }

                    return;

                /*
                case 4: // Service Component Label

                var pd = getBoolBit(d, dPosition + 16);
                var SCIdS = getBits(d, dPosition + 16 + 4, 4);
                if (pd)
                {
                    var serviceIdentifier32 = Convert.ToInt32(FIB.getBits(d, dPosition + 16 + 8, 32));
                    ServiceComponentLabel32 = getBitsASCIIString(d, dPosition + 16 + 8 + 32, 16 * 8);

                    //_loggingService.Info($"FIC: >>> Service Component Label32: Identifier: {ServiceIdentifier32}, label: {ServiceComponentLabel32}");
                } else
                {
                    var serviceIdentifier = Convert.ToInt32(FIB.getBits(d, dPosition + 16 + 8, 16));
                    ServiceComponentLabel = getBitsASCIIString(d, dPosition + 16 + 8 + 16, 16 * 8);

                    //_loggingService.Info($"FIC: >>> Service Component Label: Identifier: {ServiceIdentifier}, label: {ServiceComponentLabel}");
                }

                break;


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
                    return;
            }
        }
    }
}
