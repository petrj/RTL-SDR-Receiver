using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR
{
    public class DSPComplex
    {
        public DSPComplex(float r, float i)
        {
            Real = r;
            Imag = i;
        }

        public DSPComplex(double r, double i)
        {
            Real = (float)r;
            Imag = (float)i;
        }

        public float L1Norm()
        {
            return Math.Abs(Real) + Math.Abs(Imag);
        }

        public DSPComplex Multiply(DSPComplex cmplx)
        {
            // x⋅y=[x1y1−x2y2;x1y2+x2y1]=(x1y1−x2y2)+(x1y2+x2y1)i
            // https://www.karlin.mff.cuni.cz/~portal/komplexni_cisla/?page=algebraicky-tvar-operace

            return new DSPComplex(
                (Real * cmplx.Real - Imag * cmplx.Imag),
                (Real * cmplx.Imag + Imag * cmplx.Real));

        }

        float Real { get; set; }
        float Imag { get; set; }
    }
}
