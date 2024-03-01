using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace RTLSDR.DAB
{
    /// <summary>
    /// https://github.com/quiet/libfec/tree/master
    /// </summary>
    public class ReedSolomonErrorCorrection
    {
        /// <summary>
        /// Bits per symbol
        /// </summary>
        private int _mm { get; set; }

        /// <summary>
        /// Symbols per block
        /// </summary>
        private int _nn { get; set; }

        /// <summary>
        /// log lookup table
        /// </summary>
        private byte[] _alpha_to { get; set; }

        /// <summary>
        /// Antilog lookup table
        /// </summary>
        private byte[] _index_of { get; set; }

        /// <summary>
        /// Generator polynomial
        /// </summary>
        private byte[] _genpoly { get; set; }

        /// <summary>
        /// Number of generator roots = number of parity symbols
        /// </summary>
        private int _nroots { get; set; }

        /// <summary>
        /// First consecutive root, index form
        /// </summary>
        private int _fcr { get; set; }

        /// <summary>
        /// Primitive element, index form
        /// </summary>
        private int _prim { get; set; }

        /// <summary>
        /// prim-th root of 1, index form
        /// </summary>
        private int _iprim { get; set; }

        /// <summary>
        /// Padding bytes in shortened block
        /// </summary>
        private int _pad { get; set; }

        public ReedSolomonErrorCorrection(int symsize, int gfpoly, int fcr, int prim, int nroots, int pad)
        {
            int i, j, sr, root, iprim;

            /* Check parameter ranges */
            if (symsize < 0 || symsize > 8)
                throw new Exception("ReedSolomonErrorCorrection: bad initialization data");

            if (fcr < 0 || fcr >= (1 << symsize))
                throw new Exception("ReedSolomonErrorCorrection: bad initialization data");

            if (prim <= 0 || prim >= (1 << symsize))
                throw new Exception("ReedSolomonErrorCorrection: bad initialization data");

            if (nroots < 0 || nroots >= (1 << symsize))
                throw new Exception("ReedSolomonErrorCorrection: bad initialization data");
                 /* Can't have more roots than symbol values! */

            if (pad < 0 || pad >= ((1 << symsize) - 1 - nroots))
                throw new Exception("ReedSolomonErrorCorrection: bad initialization data");
                /* Too much padding */

            _mm = symsize;
            _nn = (1 << symsize) - 1;
            _pad = pad;

            _alpha_to = new byte[_nn + 1];
            _index_of = new byte[_nn + 1];

            /* Generate Galois field lookup tables */
            _index_of[0] = Convert.ToByte(_nn); /* log(zero) = -inf */
            _alpha_to[_nn] = 0; /* alpha**-inf = 0 */

            sr = 1;
            for (i = 0; i < _nn; i++)
            {
                _index_of[sr] = Convert.ToByte(i);
                _alpha_to[i] = Convert.ToByte(sr);
                sr <<= 1;

                if ((sr & (1 << symsize)) > 0)
                {
                    sr ^= gfpoly; // XOR
                }

                sr &= _nn;
            }

            if (sr != 1)
            {
                /* field generator polynomial is not primitive! */
                return; // TODO: throw exception?
            }

            /* Form RS code generator polynomial from its roots */
            _genpoly = new byte[nroots + 1];

            _fcr = fcr;
            _prim = prim;
            _nroots = nroots;

            /* Find prim-th root of 1, used in decoding */
            for (iprim = 1; (iprim % prim) != 0; iprim += _nn) ;

            _iprim = iprim / prim;

            _genpoly[0] = 1;

            for (i = 0, root = fcr * prim; i < nroots; i++, root += prim)
            {
                _genpoly[i + 1] = 1;

                /* Multiply rs->genpoly[] by  @**(root + x) */
                for (j = i; j > 0; j--)
                {
                    if (_genpoly[j] != 0)
                    {
                        _genpoly[j] = Convert.ToByte(_genpoly[j - 1] ^ _alpha_to[Modnn(_index_of[_genpoly[j]] + root)]);
                    }
                    else
                    {
                        _genpoly[j] = _genpoly[j - 1];
                    }
                }
                /* rs->genpoly[0] can never be zero */
                _genpoly[0] = _alpha_to[Modnn(_index_of[_genpoly[0]] + root)];
            }

            /* convert rs->genpoly[] to index form for quicker encoding */
            for (i = 0; i <= nroots; i++)
            {
                _genpoly[i] = _index_of[_genpoly[i]];
            }

        }

        /// <summary>
        /// function to reduce its argument modulo NN.
        /// </summary>
        private int Modnn(int x)
        {
            while (x >= _nn)
            {
                x -= _nn;
                x = (x >> _mm) + (x & _nn);
            }
            return x;
        }

        private int Min(int a, int b)
        {
            return a > b ? b : a;
        }

        public int DecodeRSChar(byte[] data, int[] eras_pos, int no_eras)
        {
            int deg_lambda, el, deg_omega;
            int i, j, r, k;
            byte u, q, tmp, num1, num2, den, discr_r;

            var lambda = new byte[_nroots + 1];
            var s = new byte[_nroots];  /* Err+Eras Locator poly * and syndrome poly */

            var b = new byte[_nroots + 1];
            var t = new byte[_nroots + 1];
            var omega = new byte[_nroots + 1];

            var root = new byte[_nroots];
            var reg = new byte[_nroots + 1];
            var loc = new byte[_nroots];

            int syn_error, count;

            /* form the syndromes; i.e., evaluate data(x) at roots of g(x) */
            for (i = 0; i < _nroots; i++)
            {
                s[i] = data[0];
            }

            for (j = 1; j < _nn - _pad; j++)
            {
                for (i = 0; i < _nroots; i++)
                {
                    if (s[i] == 0)
                    {
                        s[i] = data[j];
                    }
                    else
                    {
                        s[i] = Convert.ToByte(data[j] ^ _alpha_to[Modnn(_index_of[s[i]] + (_fcr + i) * _prim)]);
                    }
                }
            }

            /* Convert syndromes to index form, checking for nonzero condition */
            syn_error = 0;
            for (i = 0; i < _nroots; i++)
            {
                syn_error |= s[i];
                s[i] = _index_of[s[i]];
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
                lambda[1] = _alpha_to[Modnn(_prim * (_nn - 1 - eras_pos[0]))];
                for (i = 1; i < no_eras; i++)
                {
                    u = Convert.ToByte(Modnn(_prim * (_nn - 1 - eras_pos[i])));
                    for (j = i + 1; j > 0; j--)
                    {
                        tmp = _index_of[lambda[j - 1]];
                        if (tmp != _nn)
                            lambda[j] ^= _alpha_to[Modnn(u + tmp)];
                    }
                }
            }

            for (i = 0; i < _nroots + 1; i++)
            {
                b[i] = _index_of[lambda[i]];
            }

            /*
            * Begin Berlekamp-Massey algorithm to determine error+erasure
            * locator polynomial
            */
            r = no_eras;
            el = no_eras;
            while (++r <= _nroots)
            {   /* r is the step number */
                /* Compute discrepancy at the r-th step in poly-form */
                discr_r = 0;
                for (i = 0; i < r; i++)
                {
                    if ((lambda[i] != 0) && (s[r - i - 1] != _nn))
                    {
                        discr_r ^= _alpha_to[Modnn(_index_of[lambda[i]] + s[r - i - 1])];
                    }
                }
                discr_r = _index_of[discr_r];    /* Index form */
                if (discr_r == _nn)
                {
                    /* B(x) <-- x*B(x) */

                    for (var l = b.Length - 1; l >= 1; l--)
                    {
                        b[l] = b[l - 1];
                    }
                    b[0] = Convert.ToByte(_nn);
                }
                else
                {
                    /* 7 lines below: T(x) <-- lambda(x) - discr_r*x*b(x) */
                    t[0] = lambda[0];
                    for (i = 0; i < _nroots; i++)
                    {
                        if (b[i] != _nn)
                        {
                            t[i + 1] = Convert.ToByte(lambda[i + 1] ^ _alpha_to[Modnn(discr_r + b[i])]);
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
                        for (i = 0; i <= _nroots; i++)
                        {
                            b[i] = Convert.ToByte((lambda[i] == 0) ? _nn : Modnn(_index_of[lambda[i]] - discr_r + _nn));
                        }
                    }
                    else
                    {
                        /* 2 lines below: B(x) <-- x*B(x) */
                        for (var l = b.Length - 1; l >= 1; l--)
                        {
                            b[l] = b[l - 1];
                        }
                        b[0] = Convert.ToByte(_nn);
                    }

                    for (var l = 0; l < _nroots + 1; l++)
                    {
                        lambda[l] = t[l];
                    }
                }
            }

            /* Convert lambda to index form and compute deg(lambda(x)) */
            deg_lambda = 0;
            for (i = 0; i < _nroots + 1; i++)
            {
                lambda[i] = _index_of[lambda[i]];
                if (lambda[i] != _nn)
                {
                    deg_lambda = i;
                }
            }

            /* Find roots of the error+erasure locator polynomial by Chien search */
            for (var l = 1; l <= _nroots; l++)
            {
                reg[l] = lambda[l];
            }

            count = 0;      /* Number of roots of lambda(x) */
            for (i = 1, k = _iprim - 1; i <= _nn; i++, k = Modnn(k + _iprim))
            {
                q = 1; /* lambda[0] is always 0 */
                for (j = deg_lambda; j > 0; j--)
                {
                    if (reg[j] != _nn)
                    {
                        reg[j] = Convert.ToByte(Modnn(reg[j] + j));
                        q ^= _alpha_to[reg[j]];
                    }
                }

                if (q != 0)
                    continue; /* Not a root */
                                /* store root (index-form) and error location number */

                root[count] = Convert.ToByte(i >= 0 ? i : 256+i);
                loc[count] = Convert.ToByte(k >= 0 ? k : 256+k);
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
                    if ((s[i - j] != _nn) && (lambda[j] != _nn))
                        tmp ^= _alpha_to[Modnn(s[i - j] + lambda[j])];
                }
                omega[i] = _index_of[tmp];
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
                    if (omega[i] != _nn)
                        num1 ^= _alpha_to[Modnn(omega[i] + i * root[j])];
                }
                num2 = _alpha_to[Modnn(root[j] * (_fcr - 1) + _nn)];
                den = 0;

                /* lambda[i+1] for i even is the formal derivative lambda_pr of lambda[i] */
                for (i = Min(deg_lambda, _nroots - 1) & ~1; i >= 0; i -= 2)
                {
                    if (lambda[i + 1] != _nn)
                    {
                        den ^= _alpha_to[Modnn(lambda[i + 1] + i * root[j])];
                    }
                }

                /* Apply error to data */
                if (num1 != 0 && loc[j] >= _pad)
                {
                    data[loc[j] - _pad] ^= _alpha_to[Modnn(_index_of[num1] + _index_of[num2] + _nn - _index_of[den])];
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
