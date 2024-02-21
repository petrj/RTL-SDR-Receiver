using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR.Core
{
    public static class SByteExtensions
    {
        public static sbyte[] CloneArray(this sbyte[] array)
        {
            var arrayCopy = new sbyte[array.Length];
            Buffer.BlockCopy(array, 0, arrayCopy, 0, array.Length);
            return arrayCopy;
        }
    }
}
