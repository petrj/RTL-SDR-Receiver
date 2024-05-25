using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class FourierSinCosTable
    {
        public float[] CosTable { get; set; }
        public float[] SinTable { get; set; }

        private int _count = -1;

        private void PreCompute()
        {
            CosTable = new float[_count];
            SinTable = new float[_count];

            for (int i = 0; i < _count; i++)
            {
                var arg = 2.0 * System.Math.PI * i / _count;
                CosTable[i] = Convert.ToSingle(System.Math.Cos(arg));
                SinTable[i] = Convert.ToSingle(System.Math.Sin(arg));
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
