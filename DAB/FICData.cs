using System;
using System.Collections.Generic;
using LoggerService;

namespace DAB
{
    public class FICData
    {
        private int index { get; set; } = 0;
        private int ficno { get; set; } = 0;

        private const int bitsperBlock = 2 * 1536;
        public const int FICSize = 2304;
        public const int RATE = 4;
        public const int K = 7;
        public const int frameBits = 768;

        private byte[] Buffer { get; set; } = new byte[FICSize];

        private short[] PI_15;
        private short[] PI_16;

        private ILoggingService _loggingService;

        public FICData(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            PI_15 = getPCodes(15 - 1);
            PI_16 = getPCodes(16 - 1);
        }

        private short[,] _p_codes = new short[24, 32] {
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

        private short[] getPCodes(int x)
        {
            var res = new List<short>();
            for (int i=0;i<32;i++)
            {
                res.Add(_p_codes[x, i]);
            }
            return res.ToArray();
        }

        public void Parse(byte[] ficData, int blkno)
        {
            _loggingService.Debug($"Parsing FIC data");

            if (blkno == 1)
            {
                index = 0;
                ficno = 0;
            }

            if ((1 <= blkno) && (blkno <= 3))
            {
                for (int i = 0; i < bitsperBlock; i++)
                {
                    Buffer[index++] = ficData[i];
                    if (index >= FICSize)
                    {
                        ProcessFICInput(Buffer, ficno);
                        index = 0;
                        ficno++;
                    }
                }
            }
            else
            {
               throw new ArgumentException("Invalid ficBlock blkNo\n");
            }
        }

        private void ProcessFICInput(byte[] data, int ficNo)
        {
            try
            {
                var viterbiBlock = new byte[3072 + 24];
                var local = 0;
                int input_counter = 0;

                for (int i = 0; i < 21; i++)
                {
                    for (int k = 0; k < 32 * 4; k++)
                    {
                        if (PI_16[k % 32] != 0)
                        {
                           viterbiBlock[local] = data[input_counter++];
                        }
                        local++;
                    }
                }

                _loggingService.Info($"local: {local}");

                for (int i = 0; i < 3; i++)
                {
                    for (int k = 0; k < 32 * 4; k++)
                    {
                        if (PI_15[k % 32] != 0)
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

                var bitBuffer_out = deconvolve(viterbiBlock);

            } catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        private byte[] deconvolve(byte[] viterbiBlock)
        {
            var symbols = new int[RATE * (frameBits + (K - 1))];

            for (int i = 0; i < (frameBits + (K - 1)) * RATE; i++)
            {
                var temp = viterbiBlock[i] + 127;
                if (temp < 0) temp = 0;
                if (temp > 255) temp = 255;
                symbols[i] = temp;
            }

            //  update_viterbi_blk_GENERIC (&vp, symbols, frameBits + (K - 1));



            return new byte[0];
        }
    }
}
