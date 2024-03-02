using System;
namespace RTLSDR.DAB
{
    public class DABCRC
    {
        private uint[] _crc_lut;

        public DABCRC()
        {
            FillLUT();
        }

        public uint CalcCRC(byte[] data)
        {
            uint crc = 0x0000;

            for (var offset = 0; offset < data.Length; offset++)
            {
                crc = (crc << 8) ^ _crc_lut[(crc >> 8) ^ data[offset]];
            }

            return crc;
        }

        private void FillLUT(uint gen_polynom = 0x1021)
        {
            _crc_lut = new uint[256];

            for (int value = 0; value < 256; value++)
            {
                uint crc = Convert.ToUInt32(value << 8);

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (crc << 1) ^ gen_polynom;
                    }
                    else
                    {
                        crc = crc << 1;
                    }
                }

                _crc_lut[value] = crc;
            }
        }
    }
}
