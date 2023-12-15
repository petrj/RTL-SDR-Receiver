using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace RTLSDR
{
    public static class ComplexExtensions
    {
        public static double L1Norm(this Complex complex)
        {
            return Math.Abs(complex.Real) + Math.Abs(complex.Imaginary);
        }

        public static Complex Multiply(this Complex complex, Complex cmplx)
        {
            // x⋅y=[x1y1−x2y2;x1y2+x2y1]=(x1y1−x2y2)+(x1y2+x2y1)i
            // https://www.karlin.mff.cuni.cz/~portal/komplexni_cisla/?page=algebraicky-tvar-operace

            return new Complex(
                (complex.Real * cmplx.Real - complex.Imaginary * cmplx.Imaginary),
                (complex.Real * cmplx.Imaginary + complex.Imaginary * cmplx.Real));
        }
    }
}
