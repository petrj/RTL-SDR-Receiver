using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class Viterbi
    {
        private int _frameBits = 768;

        private const int RATE = 4;
        private const int K = 7;
        private const int NUMSTATES = 64;
        private const int RENORMALIZE_THRESHOLD = 137;
        private const int METRICSHIFT = 0;
        private const int PRECISIONSHIFT = 0;
        private const int ADDSHIFT = (8 - (K - 1));
        private static int[] _maskTable = { 128, 64, 32, 16, 8, 4, 2, 1 };

        private int[] _branchTab;

        private short[] ParTab = new short[16 * 16]
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

        public Viterbi(int frameBits)
        {
            _frameBits = frameBits;

            _branchTab = new int[NUMSTATES / 2 * RATE];

            var polys = new int[4] { 109, 79, 83, 109 };

            for (int state = 0; state < NUMSTATES / 2; state++)
            {
                for (int i = 0; i < RATE; i++)
                {
                    _branchTab[i * NUMSTATES / 2 + state] =
                    Convert.ToInt32(
                    (polys[i] < 0) ^ Parity((2 * state) & Math.Abs(polys[i])) ? 255 : 0);
                }
            }
        }

        public int FrameBits
        {
            get
            {
                return _frameBits;
            }
        }

        private bool Parity(int x)
        {
            /* Fold down to one byte */
            x ^= (x >> 16);
            x ^= (x >> 8);
            return Convert.ToBoolean(ParTab[x]);
        }

        public byte[] Deconvolve(sbyte[] viterbiBlock)
        {
            var symbols = new int[RATE * (FrameBits + (K - 1))];

            for (int i = 0; i < (FrameBits + (K - 1)) * RATE; i++)
            {
                var temp = (int)viterbiBlock[i] + 127;
                if (temp < 0) temp = 0;
                if (temp > 255) temp = 255;
                symbols[i] = temp;
            }

            var stateInfo = new ViterbiStateInfo(NUMSTATES);

            //  update_viterbi_blk_GENERIC (&vp, symbols, frameBits + (K - 1));

            var nbits = FrameBits + (K - 1);

            stateInfo.Decisions.Clear();

            for (var i = 0; i < nbits; i++)
            {
                stateInfo.Decisions.Add(new ViterbiDecision());
            }

            stateInfo.SetCurrentDecisionIndex(0);

            for (int s = 0; s < FrameBits + (K - 1); s++)
            {
                for (int i = 0; i < NUMSTATES / 2; i++)
                {
                    BFLY(i, s, symbols, stateInfo);
                }

                Renormalize(stateInfo.NewMetrics.t, RENORMALIZE_THRESHOLD);

                stateInfo.Swap();
            }

            var data = new byte[(FrameBits + (K - 1)) / 8 + 1];

            // Viterbi::chainback_viterbi(

            var endstate = 0;

            nbits = FrameBits;

            while (nbits-- != 0)
            {
                int k;

                var a = Convert.ToInt64((stateInfo.Decisions[nbits + (K - 1)].w[(endstate >> ADDSHIFT) / 32]));
                var b = ((endstate >> ADDSHIFT) % 32);

                k = Convert.ToInt32((a >> b) & 1);

                endstate = (endstate >> 1) | (k << (K - 2 + ADDSHIFT));
                data[nbits >> 3] = Convert.ToByte(endstate);
            }

            var output = new List<byte>();

            for (int i = 0; i < FrameBits; i++)
            {
                output.Add(Getbit(data[i >> 3], i & 07));
            }

            return output.ToArray();
        }

        private byte Getbit(byte v, int o)
        {
            var x = v & _maskTable[o];
            if ((v & _maskTable[o]) == _maskTable[o])
            {
                return 1;
            }
            return 0;
        }

        private void Renormalize(uint[] X, int threshold)
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
                ViterbiStateInfo vp)
        {
            int j, metric;
            long m0, m1, m2, m3, decision0, decision1;

            metric = 0;
            for (j = 0; j < RATE; j++)
            {
                metric += (_branchTab[i + j * NUMSTATES / 2] ^ syms[s * RATE + j]) >> METRICSHIFT;
            }

            metric = metric >> PRECISIONSHIFT;
            var max = ((RATE * ((256 - 1) >> METRICSHIFT)) >> PRECISIONSHIFT);

            m0 = vp.OldMetrics.t[i] + metric;
            m1 = vp.OldMetrics.t[i + NUMSTATES / 2] + (max - metric);
            m2 = vp.OldMetrics.t[i] + (max - metric);
            m3 = vp.OldMetrics.t[i + NUMSTATES / 2] + metric;

            decision0 = m0 - m1 > 0 ? 1 : 0;
            decision1 = m2 - m3 > 0 ? 1 : 0;

            vp.NewMetrics.t[2 * i] = decision0 == 1 ? Convert.ToUInt32(m1) : Convert.ToUInt32(m0);
            vp.NewMetrics.t[2 * i + 1] = decision1 == 1 ? Convert.ToUInt32(m3) : Convert.ToUInt32(m2);

            var arg = (decision0 | decision1 << 1) << ((2 * i) & 32 - 1);

            var w = i < 16 ? 0 : 1;
            vp.Decisions[s].w[w] |= Convert.ToUInt32(arg);
        }
    }
}
