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
                        //process_FIG1(data);
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
                processedBytes += Convert.ToInt32(getBits_5(data, dataPos+3) + 1);
                dataPos +=  processedBytes * 8;
            }

            return res;
        }
    }
}
