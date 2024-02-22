using System;
using System.Collections.Generic;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABDecoder
    {
        private List<byte> _buffer = null;
        private byte[] _rsPacket = new byte[120];
        private int[] _corrPos = new int[10];
        private int _frameLength = 0;
        private int _currentFrame = 0; // frame_count


        public DABDecoder(int frameLength)
        {
            _frameLength = frameLength;
            _buffer = new List<byte>();

            for (var i=0;i<_rsPacket.Length;i++)
            {
                _rsPacket[i] = 0;
            }
            for (var i = 0; i < _corrPos.Length; i++)
            {
                _corrPos[i] = 0;
            }
        }

        private int SFLength
        {
            get
            {
                return _frameLength * 5;
            }
        }

        public void AddData(byte[] data)
        {
            _buffer.AddRange(data);

            _currentFrame++;

            if (_currentFrame == 5)
            {
                // TODO: get response from DecodeSuperFrame and eventually shift frame .....
                DecodeSuperFrame(_buffer.ToArray());

                _buffer.Clear();
                _currentFrame = 0;
            }
        }

        private void DecodeSuperFrame(byte[] sf)
        {
            /*
            int total_corr_count = 0;
            bool uncorr_errors = false;

            int subch_index = SFLength / 120;

            // process all RS packets
            for (int i = 0; i < subch_index; i++)
            {
                for (int pos = 0; pos < 120; pos++)
                {
                    _rsPacket[pos] = sf[pos * subch_index + i];
                }

                // detect errors
                int corr_count = decode_rs_char(rs_handle, rs_packet, corr_pos, 0);
                if (corr_count == -1)
                    uncorr_errors = true;
                else
                    total_corr_count += corr_count;

                // correct errors
                for (int j = 0; j < corr_count; j++)
                {

                    int pos = _corrPos[j] - 135;
                    if (pos < 0)
                        continue;

                    sf[pos * subch_index + i] = _rsPacket[pos];
                }
            }
            */
        }
    }
}
