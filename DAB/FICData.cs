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
        public const int NUMSTATES = 64;
        public const int K = 7;
        public const int frameBits = 768;

        public const int METRICSHIFT = 0;
        public const int PRECISIONSHIFT = 0;
        public const int RENORMALIZE_THRESHOLD = 137;
        public const int SUBSHIFT = 0;

        public const int ADDSHIFT = (8 - (K - 1));

        static int[] maskTable = { 128, 64, 32, 16, 8, 4, 2, 1 };

        private byte[] Buffer { get; set; } = new byte[FICSize];

        private short[] PI_15;
        private short[] PI_16;

        private int[] BranchTab;

        private ILoggingService _loggingService;

        public FICData(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            PI_15 = getPCodes(15 - 1);
            PI_16 = getPCodes(16 - 1);

            var polys = new int[4] { 109, 79, 83, 109 };

            BranchTab = new int[NUMSTATES / 2 * RATE];

            for (int state = 0; state < NUMSTATES / 2; state++)
            {
                for (int i = 0; i < RATE; i++)
                {
                        BranchTab[i * NUMSTATES / 2 + state] =
                        Convert.ToInt32(
                        (polys[i] < 0) ^ Parity((2 * state) & Math.Abs(polys[i])) ? 255 : 0);
                }
            }
        }

        private bool Parity(int x)
        {
            /* Fold down to one byte */
            x ^= (x >> 16);
            x ^= (x >> 8);
            return Convert.ToBoolean(_parTab[x]);
            //  return parityb(x);
        }

        private short[] _parTab = new short[16*16]
            { 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0};

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

            var stateInfo = new ViterbiStateInfo(NUMSTATES);

            //  update_viterbi_blk_GENERIC (&vp, symbols, frameBits + (K - 1));

            var d = new Decision[frameBits + (K - 1)];
            for (var i=0; i<d.Length; i++)
            {
                d[i] = new Decision();
            }

            for (int s = 0; s < frameBits + (K - 1); s++)
            {

                for (int i = 0; i < NUMSTATES / 2; i++)
                {
                    BFLY(i, s, symbols, stateInfo, d);
                }              

                renormalize(stateInfo.new_metrics, RENORMALIZE_THRESHOLD);

                //     Swap pointers to old and new metrics
                var tmp = stateInfo.old_metrics;
                stateInfo.old_metrics = stateInfo.new_metrics;
                stateInfo.new_metrics = tmp;        
            }

            var data = new byte[(frameBits + (K - 1)) / 8 + 1];

            // Viterbi::chainback_viterbi(

            var endstate = (0 % NUMSTATES) << ADDSHIFT;

            var nbits = frameBits;

              // ???? d += (K - 1); /* Look past tail */
            while (nbits-- != 0)
            {
                int k;

                var a = Convert.ToInt32((d[nbits].W[(endstate >> ADDSHIFT) / 32]));
                var b = ((endstate >> ADDSHIFT) % 32);

                k = (a >>  b) & 1;
        
                endstate = (endstate >> 1) | (k << (K - 2 + ADDSHIFT));
                data[nbits >> 3] = Convert.ToByte(endstate >> SUBSHIFT);
            }

            var output = new List<byte>();

            for (int i = 0; i < frameBits; i++)
            {
                output.Add(Getbit(data[i >> 3], i & 07));
            }

            return output.ToArray();
        }

        private byte Getbit(byte v, int o)
        {
            var x = v & maskTable[o];
            if ((v & maskTable[o]) == maskTable[o])
            {
                return 1;
            }
            return 0;
        }

        private void renormalize(int[] X, int threshold)
        {
            int i;

            if (X[0] > threshold)
            {
                var min = X[0];
                for (i = 0; i < NUMSTATES; i++)
                {
                    if (min > X[i])
                        min = X[i];
                }

                for (i = 0; i < NUMSTATES; i++)
                {
                    X[i] -= min;
                }
            }
        }


        private void BFLY(
                int i,
                int s,
                int[] syms,
                ViterbiStateInfo vp,
                Decision[] d)
        {
            try
            {
                int j, decision0, decision1, metric, m0, m1, m2, m3;

                metric = 0;
                for (j = 0; j < RATE; j++)
                {
                    metric += (BranchTab[i + j * NUMSTATES / 2] ^ syms[s * RATE + j]) >> METRICSHIFT;
                }

                metric = metric >> PRECISIONSHIFT;
                var max = ((RATE * ((256 - 1) >> METRICSHIFT)) >> PRECISIONSHIFT);

                m0 = vp.old_metrics[i] + metric;
                m1 = vp.old_metrics[i + NUMSTATES / 2] + (max - metric);
                m2 = vp.old_metrics[i] + (max - metric);
                m3 = vp.old_metrics[i + NUMSTATES / 2] + metric;

                decision0 = Convert.ToInt32((m0 - m1) > 0);
                decision1 = Convert.ToInt32((m2 - m3) > 0);

                vp.new_metrics[2 * i] = decision0 == 1 ? m1 : m0;
                vp.new_metrics[2 * i + 1] = decision1 == 1 ? m3 : m2;

                var ind = i / 16 + s * 2;
                var arg = (decision0 | decision1 << 1) << ((2 * i) & 32 - 1);



                d[ind].W[0] |= (uint)arg;
                //d->w[i / (sizeof(uint32_t) * 8 / 2) + s * (sizeof(decision_t) / sizeof(uint32_t))] |=
                //   (decision0|decision1<<1) << ((2*i)&(sizeof(uint32_t) *8-1));
            } catch (Exception ex)
            {

            }
        }
    }
}
