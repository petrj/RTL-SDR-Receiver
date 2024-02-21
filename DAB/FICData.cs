using System;
using System.Collections.Generic;
using LoggerService;

namespace RTLSDR.DAB
{
    /*
        Free .NET DAB+ library

        -   based upon welle.io (https://github.com/AlbrechtL/welle.io)
        -   DAB documentation: https://www.etsi.org/deliver/etsi_en/300400_300499/300401/02.01.01_60/en_300401v020101p.pdf
    */

    public class FICData
    {
        private sbyte[] _buffer { get; set; } = new sbyte[FICSize];
        private int _index { get; set; } = 0;
        private int _ficno { get; set; } = 0;

        private const int BitsperBlock = 2 * 1536;
        private const int FICSize = 2304;

        private short[] _PI_15;
        private short[] _PI_16;

        private ILoggingService _loggingService;

        private List<DABService> _DABServices = new List<DABService> ();

        private FIB _fib = null;
        private FIGParser _fig = null;
        private byte[] _PRBS;
        private Viterbi _viterbi = null;

        public FICData(ILoggingService loggingService, Viterbi viterbi)
        {
            _loggingService = loggingService;
            _viterbi = viterbi;

            _fib = new FIB(_loggingService);
            _fig = new FIGParser(_loggingService,_fib, Services);

            _PI_15 = GetPCodes(15 - 1);
            _PI_16 = GetPCodes(16 - 1);

            _PRBS = new byte[viterbi.FrameBits];

            var shiftRegister = new byte[9];
            for (int i = 0; i < 9; i++)
            {
                shiftRegister[i] = 1;
            }

            for (int i = 0; i < 768; i++)
            {
                _PRBS[i] = Convert.ToByte(shiftRegister[8] ^ shiftRegister[4]);
                for (int j = 8; j > 0; j--)
                {
                    shiftRegister[j] = shiftRegister[j - 1];
                }

                shiftRegister[0] = _PRBS[i];
            }
        }

        public List<DABService> Services
        {
            get
            {
                return _DABServices;
            }
        }

        private short[,] PCodes = new short[24, 32] {
            { 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0},// 1
            { 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0},// 2
            { 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0},// 3
            { 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0},// 4
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0},// 5
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0},// 6
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0},// 7
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 8
            { 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 9
            { 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 10
            { 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 11
            { 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0},// 12
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0},// 13
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0},// 14
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0},// 15
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 16
            { 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 17
            { 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 18
            { 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 19
            { 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0},// 20
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0},// 21
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0},// 22
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0},// 23
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1} // 24
        };

        private short[] PI_X = new short[24] {
                1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0,
                1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0
            };

        private short[] GetPCodes(int x)
        {
            var res = new List<short>();
            for (int i=0;i<32;i++)
            {
                res.Add(PCodes[x, i]);
            }
            return res.ToArray();
        }

        public void Parse(Dictionary<int, sbyte[]> ficData)
        {
            foreach (var kvp in ficData)
            {
                ProcessFICInput(kvp.Value, kvp.Key-1);
            }
        }

        public void Parse(sbyte[] ficData, int blkno)
        {
            //_loggingService.Debug($"Parsing FIC data");

            if (blkno == 1)
            {
                _index = 0;
                _ficno = 0;
            }

            if ((1 <= blkno) && (blkno <= 3))
            {
                for (int i = 0; i < BitsperBlock; i++)
                {
                    _buffer[_index++] = ficData[i];
                    if (_index >= FICSize)
                    {
                        ProcessFICInput(_buffer, _ficno);
                        _index = 0;
                        _ficno++;
                    }
                }
            }
            else
            {
               throw new ArgumentException("Invalid ficBlock blkNo\n");
            }
        }

        private void ProcessFICInput(sbyte[] data, int ficNo)
        {
            try
            {
                var viterbiBlock = new sbyte[3072 + 24];
                var local = 0;
                int input_counter = 0;

                for (int i = 0; i < 21; i++)
                {
                    for (int k = 0; k < 32 * 4; k++)
                    {
                        if (_PI_16[k % 32] != 0)
                        {
                           viterbiBlock[local] = data[input_counter++];
                        }
                        local++;
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    for (int k = 0; k < 32 * 4; k++)
                    {
                        if (_PI_15[k % 32] != 0)
                        {
                            viterbiBlock[local] = data[input_counter++];
                        }
                        local++;
                    }
                }

                for (int k = 0; k < 24; k++)
                {
                    if (PI_X[k] != 0)
                    {
                        viterbiBlock[local] = data[input_counter++];
                    }
                    local++;
                }

                var bitBuffer_out = _viterbi.Deconvolve(viterbiBlock);

                for (var i=0;i< _viterbi.FrameBits;i++)
                {
                    bitBuffer_out[i] ^= _PRBS[i];
                }

                for (var i = _ficno * 3; i < _ficno * 3 + 3; i++)
                {
                    var ficPartBuffer = new List<byte>();
                    for (var j=0;j<256;j++)
                    {
                        ficPartBuffer.Add(bitBuffer_out[(i % 3) * 256 + j]);
                    }

                    var crcvalid = CheckCRC(ficPartBuffer.ToArray());

                    if (crcvalid)
                    {
                        _fib.Parse(ficPartBuffer.ToArray(), _ficno);
                    } else
                    {
                        //_loggingService.Info("BAD FIC CRC");
                    }

                }

            } catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        public static bool CheckCRC(byte[] data)
        {
            var size = data.Length;
            var crcPolynome = new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 }; // MSB .. LSB
            byte[] b = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                b[i] = 1;
            }

            for (int i = 0; i < size; i++)
            {
                var d = data[i];
                if (i >= size - 16)
                {
                    d ^= 1;
                }

                if ((b[0] ^ d) == 1)
                {
                    for (int f = 0; f < 15; f++)
                    {
                        b[f] = Convert.ToByte(crcPolynome[f] ^ b[f + 1]);
                    }
                    b[15] = 1;
                }
                else
                {
                    //memmove(&b[0], &b[1], sizeof(uint8_t) * 15); // Shift
                    for (int j=0;j<15;j++)
                    {
                        b[j] = b[j + 1];
                    }
                    b[15] = 0;
                }
            }

            uint crc = 0;
            for (int i = 0; i < 16; i++)
            {
                crc |= Convert.ToUInt32(b[i] << i);
            }
            return crc == 0;
        }

    }
}
