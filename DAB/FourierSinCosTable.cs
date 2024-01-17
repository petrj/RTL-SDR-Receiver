using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAB
{
    public class FourierSinCosTable
    {
        public double[] CosTable { get; set; }
        public double[] SinTable { get; set; }

        private int _count = -1;

        private void PreCompute()
        {
            CosTable = new double[_count];
            SinTable = new double[_count];

            for (int i = 0; i < _count; i++)
            {
                var arg = 2.0 * System.Math.PI * i / _count;
                CosTable[i] = System.Math.Cos(arg);
                SinTable[i] = System.Math.Sin(arg);
            }
        }

        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                var oldCount = _count;
                _count = value;
                if (_count != oldCount)
                {
                    PreCompute();
                }
            }
        }
    }
}
