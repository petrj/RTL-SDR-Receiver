using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABDecoder
    {
        private List<byte> _sf_raw_buffer = new List<byte>();
        private List<byte> _sf_buffer = new List<byte>();
        private int _currentBufferPos = 0; // frame_count

        public void AddData(byte[] data, int len)
        {

        }
    }
}
