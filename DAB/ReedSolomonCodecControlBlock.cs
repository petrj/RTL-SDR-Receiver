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
        public int mm { get; set; }              /* Bits per symbol */
        public int nn { get; set; }              /* Symbols per block (= (1<<mm)-1) */
        public byte[] alpha_to { get; set; }     /* log lookup table */
        public byte[] index_of { get; set; }     /* Antilog lookup table */
        public byte[] genpoly { get; set; }      /* Generator polynomial */
        public int nroots { get; set; }          /* Number of generator roots = number of parity symbols */
        public int fcr { get; set; }             /* First consecutive root, index form */
        public int prim { get; set; }            /* Primitive element, index form */
        public int iprim { get; set; }           /* prim-th root of 1, index form */
        public int pad { get; set; }             /* Padding bytes in shortened block */

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
            nn = (1 << symsize) - 1;
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

                if ((sr & (1 << symsize)) > 0)
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
            for (iprim = 1; (iprim % prim) != 0; iprim += nn) ;

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
                        genpoly[j] = Convert.ToByte(genpoly[j - 1] ^ alpha_to[modnn(index_of[genpoly[j]] + root)]);
                    }
                    else
                    {
                        genpoly[j] = genpoly[j - 1];
                    }
                }
                /* rs->genpoly[0] can never be zero */
                genpoly[0] = alpha_to[modnn(index_of[genpoly[0]] + root)];
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
        public byte modnn(int x)
        {
            while (x >= nn)
            {
                x -= nn;
                x = (x >> mm) + (x & nn);
            }
            return Convert.ToByte(x);
        }

        private int min(int a, int b)
        {
            return a > b ? b : a;
        }

        // decode_rs.h
        public int decode_rs_char(byte[] data, int[] eras_pos, int no_eras)
        {
            int deg_lambda, el, deg_omega;
            int i, j, r, k;
            byte u, q, tmp, num1, num2, den, discr_r;

            var lambda = new byte[nroots + 1];
            var s = new byte[nroots];  /* Err+Eras Locator poly * and syndrome poly */

            var b = new byte[nroots + 1];
            var t = new byte[nroots + 1];
            var omega = new byte[nroots + 1];

            var root = new byte[nroots];
            var reg = new byte[nroots + 1];
            var loc = new byte[nroots];

            int syn_error, count;

            /* form the syndromes; i.e., evaluate data(x) at roots of g(x) */
            for (i = 0; i < nroots; i++)
            {
                s[i] = data[0];
            }

            for (j = 1; j < nn - pad; j++)
            {
                for (i = 0; i < nroots; i++)
                {
                    if (s[i] == 0)
                    {
                        s[i] = data[j];
                    }
                    else
                    {
                        s[i] = Convert.ToByte(data[j] ^ alpha_to[modnn(index_of[s[i]] + (fcr + i) * prim)]);
                    }
                }
            }

            /* Convert syndromes to index form, checking for nonzero condition */
            syn_error = 0;
            for (i = 0; i < nroots; i++)
            {
                syn_error |= s[i];
                s[i] = index_of[s[i]];
            }

            if (syn_error == 0)
            {
                /* if syndrome is zero, data[] is a codeword and there are no
                 * errors to correct. So return data[] unmodified
                 */
                return 0;
            }

            lambda[0] = 1;

            // not necessary for C#?
            for (var l = 1; l < lambda.Length; l++)
                lambda[l] = 0;

            if (no_eras > 0)
            {
                /* Init lambda to be the erasure locator polynomial */
                lambda[1] = alpha_to[modnn(prim * (nn - 1 - eras_pos[0]))];
                for (i = 1; i < no_eras; i++)
                {
                    u = modnn(prim * (nn - 1 - eras_pos[i]));
                    for (j = i + 1; j > 0; j--)
                    {
                        tmp = index_of[lambda[j - 1]];
                        if (tmp != nn)
                            lambda[j] ^= alpha_to[modnn(u + tmp)];
                    }
                }
            }

            for (i = 0; i < nroots + 1; i++)
            {
                b[i] = index_of[lambda[i]];
            }

            /*
           * Begin Berlekamp-Massey algorithm to determine error+erasure
           * locator polynomial
           */
            r = no_eras;
            el = no_eras;
            while (++r <= nroots)
            {   /* r is the step number */
                /* Compute discrepancy at the r-th step in poly-form */
                discr_r = 0;
                for (i = 0; i < r; i++)
                {
                    if ((lambda[i] != 0) && (s[r - i - 1] != nn))
                    {
                        discr_r ^= alpha_to[modnn(index_of[lambda[i]] + s[r - i - 1])];
                    }
                }
                discr_r = index_of[discr_r];    /* Index form */
                if (discr_r == nn)
                {
                    /* B(x) <-- x*B(x) */

                    for (var l = b.Length - 1; l >= 1; l--)
                    {
                        b[l] = b[l - 1];
                    }
                    b[0] = Convert.ToByte(nn);
                }
                else
                {
                    /* 7 lines below: T(x) <-- lambda(x) - discr_r*x*b(x) */
                    t[0] = lambda[0];
                    for (i = 0; i < nroots; i++)
                    {
                        if (b[i] != nn)
                        {
                            t[i + 1] = Convert.ToByte(lambda[i + 1] ^ alpha_to[modnn(discr_r + b[i])]);
                        }
                        else
                        {
                            t[i + 1] = lambda[i + 1];
                        }
                    }
                    if (2 * el <= r + no_eras - 1)
                    {
                        el = r + no_eras - el;
                        /*
                         * 2 lines below: B(x) <-- inv(discr_r) *
                         * lambda(x)
                         */
                        for (i = 0; i <= nroots; i++)
                        {
                            b[i] = Convert.ToByte((lambda[i] == 0) ? nn : modnn(index_of[lambda[i]] - discr_r + nn));
                        }
                    }
                    else
                    {
                        /* 2 lines below: B(x) <-- x*B(x) */
                        for (var l = b.Length - 1; l >= 1; l--)
                        {
                            b[l] = b[l - 1];
                        }
                        b[0] = Convert.ToByte(nn);
                    }

                    for (var l = 0; l < nroots + 1; l++)
                    {
                        lambda[l] = t[l];
                    }
                }
            }

            /* Convert lambda to index form and compute deg(lambda(x)) */
            deg_lambda = 0;
            for (i = 0; i < nroots + 1; i++)
            {
                lambda[i] = index_of[lambda[i]];
                if (lambda[i] != nn)
                {
                    deg_lambda = i;
                }
            }

            /* Find roots of the error+erasure locator polynomial by Chien search */

            for (var l = 1; l < nroots; l++)
            {
                reg[l] = lambda[l];
            }

            count = 0;      /* Number of roots of lambda(x) */
            for (i = 1, k = iprim - 1; i <= nn; i++, k = modnn(k + iprim))
            {
                q = 1; /* lambda[0] is always 0 */
                for (j = deg_lambda; j > 0; j--)
                {
                    if (reg[j] != nn)
                    {
                        reg[j] = modnn(reg[j] + j);
                        q ^= alpha_to[reg[j]];
                    }
                }

                if (q != 0)
                    continue; /* Not a root */
                /* store root (index-form) and error location number */

                root[count] = Convert.ToByte(i);
                loc[count] = Convert.ToByte(k);
                /* If we've already found max possible roots,
                 * abort the search to save time
                 */
                if (++count == deg_lambda)
                    break;
            }

            if (deg_lambda != count)
            {
                /*
                 * deg(lambda) unequal to number of roots => uncorrectable
                 * error detected
                 */
                return -1;
            }

            /*
             * Compute err+eras evaluator poly omega(x) = s(x)*lambda(x) (modulo
             * x**NROOTS). in index form. Also find deg(omega).
             */
            deg_omega = deg_lambda - 1;
            for (i = 0; i <= deg_omega; i++)
            {
                tmp = 0;
                for (j = i; j >= 0; j--)
                {
                    if ((s[i - j] != nn) && (lambda[j] != nn))
                        tmp ^= alpha_to[modnn(s[i - j] + lambda[j])];
                }
                omega[i] = index_of[tmp];
            }

            /*
             * Compute error values in poly-form. num1 = omega(inv(X(l))), num2 =
             * inv(X(l))**(FCR-1) and den = lambda_pr(inv(X(l))) all in poly-form
             */
            for (j = count - 1; j >= 0; j--)
            {
                num1 = 0;
                for (i = deg_omega; i >= 0; i--)
                {
                    if (omega[i] != nn)
                        num1 ^= alpha_to[modnn(omega[i] + i * root[j])];
                }
                num2 = alpha_to[modnn(root[j] * (fcr - 1) + nn)];
                den = 0;

                /* lambda[i+1] for i even is the formal derivative lambda_pr of lambda[i] */
                for (i = min(deg_lambda, nroots - 1) & ~1; i >= 0; i -= 2)
                {
                    if (lambda[i + 1] != nn)
                    {
                        den ^= alpha_to[modnn(lambda[i + 1] + i * root[j])];
                    }
                }

                /* Apply error to data */
                if (num1 != 0 && loc[j] >= pad)
                {
                    data[loc[j] - pad] ^= alpha_to[modnn(index_of[num1] + index_of[num2] + nn - index_of[den])];
                }
            }

            for (i = 0; i < count; i++)
            {
                eras_pos[i] = loc[i];
            }

            return count;
        }
    }
}
