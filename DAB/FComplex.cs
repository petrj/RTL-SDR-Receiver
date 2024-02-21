using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    /*
        Free .NET DAB+ library
    */
    public struct FComplex
    {
        public float Real { get; set; }
        public float Imaginary { get; set; }

        public FComplex(float real, float imag)
        {
            Real = real;
            Imaginary = imag;
        }

        public FComplex(double real, double imag)
        {
            Real = Convert.ToSingle(real);
            Imaginary = Convert.ToSingle(imag);
        }

        public static FComplex[] CloneComplexArray(FComplex[] complexArray)
        {
            var res = new FComplex[complexArray.Length];
            //Array.Copy(complexArray, 0, res, 0, complexArray.Length);

            for (var i = 0; i < complexArray.Length; i++)
            {
                res[i] = complexArray[i].Clone();
            }
            return res;
        }

        public FComplex Clone()
        {
            return new FComplex(Real, Imaginary);
        }

        public static FComplex Multiply(FComplex a, FComplex b)
        {
            // x⋅y=[x1y1−x2y2;x1y2+x2y1]=(x1y1−x2y2)+(x1y2+x2y1)i
            // https://www.karlin.mff.cuni.cz/~portal/komplexni_cisla/?page=algebraicky-tvar-operace

            return new FComplex(
                (a.Real * b.Real - a.Imaginary * b.Imaginary),
                (a.Real * b.Imaginary + a.Imaginary * b.Real));
        }

        public FComplex Conjugated()
        {
            return new FComplex(Real, -Imaginary);
        }

        public void Multiply(float f)
        {
            Real *= f;
            Imaginary *= f;
        }

        public float L1Norm()
        {
            return Math.Abs(Real) + Math.Abs(Imaginary);
        }

        public void Add(FComplex complex)
        {
            Real += complex.Real;
            Imaginary += complex.Imaginary;
        }

        public float Abs()
        {
            if (Real == 0 && Imaginary == 0)
                return 0;

            return Convert.ToSingle(Math.Sqrt(Math.Pow(Real, 2) + Math.Pow(Imaginary, 2)));
        }

        public float PhaseAngle(bool degrees = false)
        {
            if (Real == 0 && Imaginary == 0)
            {
                return 0;
            }

            // Calculate the phase angle using the arctangent function
            double phaseAngle = Math.Atan2(Imaginary, Real);

            if (degrees)
            {
                phaseAngle = (phaseAngle * (180.0 / Math.PI));
            }

            return Convert.ToSingle(phaseAngle);
        }

        public override string ToString()
        {
            return $"{Real.ToString("N5")} {Imaginary.ToString("N5")}i";
        }
    }
}
