using System;
using System.Collections.Generic;

namespace RTLSDR.DAB
{
    public class FrequencyInterleaver
    {
        private int[] _permTable;

        public FrequencyInterleaver(int T_u, int K)
        {
            // mode 1:
            _permTable = CreateMapper(T_u, 511, 256, 256 + K);
        }

        //  according to the standard, the map is a function from
        //  0 .. 1535->-768 .. 768 (with exclusion of {0})
        public int MapIn(int n)
        {
            return _permTable[n];
        }

        public static int[] CreateMapper(int T_u, int V1, int lwb, int upb)
        {
            var tmp = new List<int>();
            var v = new int[T_u];

            var index = 0;
            int  i;

            tmp.Add(0);
            for (i = 1; i < T_u; i++)
            {
                tmp.Add((13 * tmp[i - 1] + V1) % T_u);
            }

            for (i = 0; i < T_u; i++)
            {
                if (tmp[i] == T_u / 2)
                    continue;
                if ((tmp[i] < lwb) || (tmp[i] > upb))
                    continue;
                //  we now have a table with values from lwb .. upb

                v[index++] = tmp[i] - T_u / 2;
                //  we now have a table with values from lwb - T_u / 2 .. lwb + T_u / 2
            }

            return v;
        }


    }
}
