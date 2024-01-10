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
        private int[,] ProtLevel = new int[64,3]  // table 8: Sub-channel size for service components as a function
        {
                // sub chanel size - protection level - Bitrate (kbit/s)
                {16,5,32},  // Index 0
                {21,4,32},
                {24,3,32},
                {29,2,32},
                {35,1,32},  // Index 4
                {24,5,48},
                {29,4,48},
                {35,3,48},
                {42,2,48},
                {52,1,48},  // Index 9
                {29,5,56},
                {35,4,56},
                {42,3,56},
                {52,2,56},
                {32,5,64},  // Index 14
                {42,4,64},
                {48,3,64},
                {58,2,64},
                {70,1,64},
                {40,5,80},  // Index 19
                {52,4,80},
                {58,3,80},
                {70,2,80},
                {84,1,80},
                {48,5,96},  // Index 24
                {58,4,96},
                {70,3,96},
                {84,2,96},
                {104,1,96},
                {58,5,112}, // Index 29
                {70,4,112},
                {84,3,112},
                {104,2,112},
                {64,5,128},
                {84,4,128}, // Index 34
                {96,3,128},
                {116,2,128},
                {140,1,128},
                {80,5,160},
                {104,4,160},    // Index 39
                {116,3,160},
                {140,2,160},
                {168,1,160},
                {96,5,192},
                {116,4,192},    // Index 44
                {140,3,192},
                {168,2,192},
                {208,1,192},
                {116,5,224},
                {140,4,224},    // Index 49
                {168,3,224},
                {208,2,224},
                {232,1,224},
                {128,5,256},
                {168,4,256},    // Index 54
                {192,3,256},
                {232,2,256},
                {280,1,256},
                {160,5,320},
                {208,4,320},    // index 59
                {280,2,320},
                {192,5,384},
                {280,3,384},
                {416,1,384}
            };

        private ILoggingService _loggingService;

        public FIB(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event EventHandler ServiceFound;
        public event EventHandler EnsembleFound;
        public event EventHandler SubChannelFound;
        public event EventHandler ServiceComponentFound;
        public delegate void ServiceFoundEventHandler(object sender, ServiceFoundEventArgs e);
        public delegate void SubChannelFoundEventHandler(object sender, SubChannelFoundEventArgs e);
        public delegate void EnsembleFoundEventHandler(object sender, EnsembleFoundEventArgs e);
        public delegate void ServiceComponentFoundEventHandler(object sender, ServiceComponentFoundEventArgs e);

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

            while (processedBytes < 30)
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
                    dataPos += Convert.ToInt32(FIGLength * 8);
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
            var cn = FIB.GetBitsBool(d, dPosition + 8);
            var oe = FIB.GetBitsBool(d, dPosition + 8 + 1);
            var pd = FIB.GetBitsBool(d, dPosition + 8 + 2);
            var ext = FIB.GetBitsNumber(d, dPosition + 8 + 3, 5);

            var used = 2;
            switch (ext)
            {
                case 1: // Basic sub-channel organization see [0] 6.2.1
                        // mapping between the sub channel identifications and the positions in the relevant CIF

                    while (used < length - 1)
                    {
                        used = ParseFIG0Ext1(d, used, pd, dPosition);
                    }

                    break;

                case 2: // Basic service and service component definition 6.3.1

                    while (used < length - 1)
                    {
                        used = ParseFIG0Ext2(d, used, pd, dPosition);
                    }

                    break;
            }
        }

        /// <summary>
        /// Parses the FIG 0 ext1.
        /// </summary>
        /// <returns>offset in bytes</returns>
        /// <param name="d">input bitByte array</param>
        /// <param name="offset">offset in bytes in d</param>
        /// <param name="dPosition">start position of bits in d</param>
        public int ParseFIG0Ext1(byte[] d, int offset, bool pd, int dPosition = 0)
        {
            var bitOffset = offset * 8;

            var subChId = GetBitsNumber(d, dPosition + bitOffset, 6);
            var startAdr = GetBitsNumber(d, dPosition + bitOffset + 6, 10);
            uint length = 0;

            var shortLongSwitch = GetBitsBool(d, dPosition + bitOffset + 16);

            if (shortLongSwitch)
            {
                // short form
                var tableIndex = GetBitsNumber(d, dPosition + bitOffset + 18, 6);

                length = Convert.ToUInt32(ProtLevel[tableIndex,0]);

                bitOffset += 24;
            } else
            {
                // parse long form

                length = GetBitsNumber(d, dPosition + bitOffset + 22, 10);

                bitOffset += 32;
            }

            if (SubChannelFound != null)
            {
                SubChannelFound(this, new SubChannelFoundEventArgs()
                {
                     SubChannel =new SubChannelDescriptor()
                     {
                          StartAddr = startAdr,
                          SubChId = subChId,
                          Length  = length
                     }
                });
            }

            return bitOffset / 8;   // we return bytes
        }

        public int ParseFIG0Ext2(byte[] d, int offset, bool pd, int dPosition = 0)
        {
            var bitOffset = offset * 8;

            var service = new ServiceComponentDefinition();

            if (pd)
            {
                // 32 bits

                service.ExtendedCountryCode = EBUEncoding.GetString(GetBitBytes(d, dPosition + bitOffset, 8));
                service.CountryId = EBUEncoding.GetString(GetBitBytes(d, dPosition + bitOffset + 8, 4));
                service.ServiceNumber = GetBitsNumber(d, dPosition + bitOffset + 12, 20);

                bitOffset += 32;
            }  else
            {
                // 16 bits

                service.CountryId = EBUEncoding.GetString(GetBitBytes(d, dPosition + bitOffset, 4));
                service.ServiceNumber = GetBitsNumber(d, dPosition + bitOffset + 4, 12);

                bitOffset += 16;
            }

            var numberOfServices = GetBitsNumber(d, dPosition + bitOffset + 4, 4);
            bitOffset += 8;

            for (var i = 0; i < numberOfServices; i++)
            {
                var tMId = GetBitsNumber(d, dPosition + bitOffset, 2);
                switch (tMId)
                {
                    case 0: //  (MSC stream audio)
                        service.Components.Add(new MSCStreamAudioDescription()
                        {
                            AudioServiceComponentType = GetBitsNumber(d, dPosition + bitOffset + 2, 6),
                            SubChId = GetBitsNumber(d, dPosition + bitOffset + 8, 6),
                            Primary = GetBitsBool(d, dPosition + bitOffset + 14),
                            AccessControl = GetBitsBool(d, dPosition + bitOffset + 15)
                        });
                        break;
                    case 1: //  (MSC stream data)
                        service.Components.Add(new MSCStreamDataDescription()
                        {
                            DataServiceComponentType = GetBitsNumber(d, dPosition + bitOffset + 2, 6),
                            SubChId = GetBitsNumber(d, dPosition + bitOffset + 8, 6),
                            Primary = GetBitsBool(d, dPosition + bitOffset + 14),
                            AccessControl = GetBitsBool(d, dPosition + bitOffset + 15)
                        });
                        break;
                    case 3: //  (MSC packet data)
                        service.Components.Add(new MSCPacketDataDescription()
                        {
                            ServiceComponentIdentifier = GetBitsNumber(d, dPosition + bitOffset + 2, 12),
                            Primary = GetBitsBool(d, dPosition + bitOffset + 14),
                            AccessControl = GetBitsBool(d, dPosition + bitOffset + 15)
                        });
                        break;
                }
                bitOffset += 16;
            }

            if (ServiceComponentFound != null)
            {
                ServiceComponentFound(this, new ServiceComponentFoundEventArgs()
                {
                    ServiceComponent = service
                });
            }

            return bitOffset / 8;
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
                             Service = new ProgrammeServiceDescriptor()
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
                            Service = new ProgrammeServiceDescriptor()
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
// [0] ETSI EN 300 401 V2.1.1 (2017-01)