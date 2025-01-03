﻿using System;
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
        public event EventHandler OnServiceFound = null;
        public event EventHandler OnProcessedFICCountChanged = null;

        private const int BitsperBlock = 2 * 1536;
        private const int FICSize = 2304;

        private short[] _PI_15;
        private short[] _PI_16;

        private ILoggingService _loggingService;

        public int FICProcessedCountWithValidCRC { get; set; } = 0;
        public int FICProcessedCountWithInValidCRC { get; set; } = 0;

        private int _fic_decode_success_ratio = 0;

        private List<sbyte> _FICBuffer = new List<sbyte>();
        private int _currentFICNo = 0;

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
            _fig.OnServiceFound += _fig_OnServiceFound;

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

        private void _fig_OnServiceFound(object sender, EventArgs e)
        {
            if (OnServiceFound != null && (e is DABServiceFoundEventArgs))
            {
                OnServiceFound(this, e);
            }
        }

        public int FICCount
        {
            get
            {
                return FICProcessedCountWithInValidCRC + FICProcessedCountWithValidCRC;
            }
        }

        public List<DABService> Services
        {
            get
            {
                return _DABServices;
            }
        }

        public Dictionary<int,int> FigTypesFound
        {
            get
            {
                return _fib.FigTypesFound;
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

        public void ParseAllBlocksData(sbyte[] ficData)
        {
            var FICBlock = new sbyte[FICSize];

            for (var i = 0; i < 4; i++)
            {
                Buffer.BlockCopy(ficData, i * FICSize, FICBlock, 0, FICSize);
                ProcessFICInput(FICBlock, i);
            }
        }

        public void ParseData(FICQueueItem item)
        {
            if (item.FicNo == 0)
            {
                _FICBuffer.Clear();
                _currentFICNo = 0;
            }

            _FICBuffer.AddRange(item.Data);

            while (_FICBuffer.Count >= FICSize)
            {
                var ficBlock = _FICBuffer.GetRange(0, FICSize).ToArray();
                ProcessFICInput(ficBlock, _currentFICNo);
                _FICBuffer.RemoveRange(0, FICSize);
                _currentFICNo++;
            }
        }

        public int FicDecodeRatioPercent
        {
            get
            {
                return _fic_decode_success_ratio * 10;
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

                for (var i = ficNo * 3; i < ficNo * 3 + 3; i++)
                {
                    var ficPartBuffer = new List<byte>();
                    for (var j=0;j<256;j++)
                    {
                        ficPartBuffer.Add(bitBuffer_out[(i % 3) * 256 + j]);
                    }

                    var crcvalid = CheckCRC(ficPartBuffer.ToArray());

                    if (crcvalid)
                    {
                        FICProcessedCountWithValidCRC++;
                        _fib.Parse(ficPartBuffer.ToArray());

                        //_loggingService.Debug($"Valid FIC count: {_validCRCCount}");

                        if (_fic_decode_success_ratio < 10)
                        {
                            _fic_decode_success_ratio++;
                        }
                    }
                    else
                    {
                        FICProcessedCountWithInValidCRC++;

                        if (_fic_decode_success_ratio > 0)
                        {
                            _fic_decode_success_ratio--;
                        }
                    }

                    if (OnProcessedFICCountChanged != null)
                    {
                        OnProcessedFICCountChanged(this, new EventArgs());
                    }
                }

            } catch (Exception ex)
            {
                _loggingService.Error(ex);
                return;
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
