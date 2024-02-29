using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace RTLSDR.DAB
{
    /// <summary>
    /// https://github.com/quiet/libfec/blob/master/rs-common.h
    /// </summary>
    public class ReedSolomonCodecControlBlock
    {
        int mm;              /* Bits per symbol */
        int nn;              /* Symbols per block (= (1<<mm)-1) */
        byte[] alpha_to;     /* log lookup table */
        byte[] index_of;     /* Antilog lookup table */
        byte[] genpoly;      /* Generator polynomial */
        int nroots;     /* Number of generator roots = number of parity symbols */
        int fcr;        /* First consecutive root, index form */
        int prim;       /* Primitive element, index form */
        int iprim;      /* prim-th root of 1, index form */
        int pad;        /* Padding bytes in shortened block */

        // init_rs_char.c
        public ReedSolomonCodecControlBlock(int symsize, int gfpoly, int fcr, int prim, int nroots, int pad)
        {
            int i, j, sr, root, iprim;

            /* Check parameter ranges */
            if (symsize < 0 || symsize > 8)
            {
                return;
            }

            if (fcr < 0 || fcr >= (1 << symsize))
                return;

            if (prim <= 0 || prim >= (1 << symsize))
                return;

            if (nroots < 0 || nroots >= (1 << symsize))
                return; /* Can't have more roots than symbol values! */

            if (pad < 0 || pad >= ((1 << symsize) - 1 - nroots))
                return; /* Too much padding */

            mm = symsize;
            nn = (1<<symsize)-1;
            this.pad = pad;

            alpha_to = new byte[nn + 1];
            index_of = new byte[nn + 1];

            /* Generate Galois field lookup tables */
            index_of[0] = Convert.ToByte(nn); /* log(zero) = -inf */
            alpha_to[nn] = 0; /* alpha**-inf = 0 */

            sr = 1;
            for (i = 0; i < nn; i++)
            {
                index_of[sr] = Convert.ToByte(i);
                alpha_to[i] = Convert.ToByte(sr);
                sr <<= 1;

                if ((sr & (1 << symsize))>0)
                {
                    sr ^= gfpoly; // XOR
                }

                sr &= nn;
            }

            if (sr != 1)
            {
                /* field generator polynomial is not primitive! */
                return; // TODO: throw exception?
            }

            /* Form RS code generator polynomial from its roots */
            this.genpoly = new byte[nroots + 1];

            this.fcr = fcr;
            this.prim = prim;
            this.nroots = nroots;

            /* Find prim-th root of 1, used in decoding */
            for (iprim = 1; (iprim % prim) != 0; iprim += nn);

            iprim = iprim / prim;

            genpoly[0] = 1;

            for (i = 0, root = fcr * prim; i < nroots; i++, root += prim)
            {
                genpoly[i + 1] = 1;

                /* Multiply rs->genpoly[] by  @**(root + x) */
                for (j = i; j > 0; j--)
                {
                    if (genpoly[j] != 0)
                    {
                        genpoly[j] = Convert.ToByte(genpoly[j - 1] ^ alpha_to[modnn(this, index_of[genpoly[j]] + root)]);
                    }
                    else
                    {
                        genpoly[j] = genpoly[j - 1];
                    }
                }
                /* rs->genpoly[0] can never be zero */
                genpoly[0] = alpha_to[modnn(this, index_of[genpoly[0]] + root)];
            }

            /* convert rs->genpoly[] to index form for quicker encoding */
            for (i = 0; i <= nroots; i++)
            {
                genpoly[i] = index_of[genpoly[i]];
            }
        }

        /// <summary>
        /// function to reduce its argument modulo NN.
        /// </summary>
        /// <param name="rs"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        public static int modnn(ReedSolomonCodecControlBlock rs,int x)
        {
            while (x >= rs.nn)
            {
                x -= rs.nn;
                x = (x >> rs.mm) + (x & rs.nn);
            }
            return x;
        }
    }
}
