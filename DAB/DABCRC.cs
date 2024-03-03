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

        /// <summary>
        /// Calculates the crc (CRC16_CCITT)
        /// </summary>
        /// <returns>The crc.</returns>
        /// <param name="data">Data.</param>
        public uint CalcCRC(byte[] data)
        {
            uint crc = 0xFFFF;

            for (var offset = 0; offset < data.Length; offset++)
            {
                var a = ((crc << 8) & 0xFFFF);
                var b = _crc_lut[(crc >> 8) ^ data[offset]];
                var c = a ^ b;

                crc = ((crc << 8) & 0xFFFF) ^ _crc_lut[(crc >> 8) ^ data[offset]];
            }

            return Convert.ToUInt32(~crc & 0xFFFF);
        }

        private void FillLUT(uint gen_polynom = 0x1021)
        {
            _crc_lut = new uint[256];

            for (int value = 0; value < 256; value++)
            {
                uint crc = Convert.ToUInt32((value << 8) & 0xFFFF); // 16 bit number

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = ((crc << 1) & 0xFFFF) ^ gen_polynom;
                    }
                    else
                    {
                        crc = (crc << 1) & 0xFFFF;
                    }
                }

                _crc_lut[value] = crc;
            }
        }
    }
}
