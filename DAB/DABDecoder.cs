using RTLSDR.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABDecoder
    {
        private EEPProtection _EEPProtection;
        private Viterbi _MSCViterbi;
        private EnergyDispersal _energyDispersal;

        private List<byte> _buffer = null;
        private byte[] _rsPacket = new byte[120];
        private int[] _corrPos = new int[10];
        private int _frameLength = 0;
        private int _currentFrame = 0; // frame_count

        private int _fragmentSize = 0;

        private int _countforInterleaver = 0;
        private int _interleaverIndex = 0;

        private int _bitRate = 0;

        private sbyte[] InterleaveMap = new sbyte[16] { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };
        private sbyte[,] _interleaveData = null;
        private sbyte[] _tempX = null;

        private ReedSolomonCodecControlBlock _rs;

        private ConcurrentQueue<byte[]> _DABQueue;

        public DABDecoder(DABSubChannel dABSubChannel, int CUSize, ConcurrentQueue<byte[]> queue)
        {
            _DABQueue = queue;

            _MSCViterbi = new Viterbi(dABSubChannel.Bitrate*24);
            _EEPProtection = new EEPProtection(dABSubChannel.Bitrate, EEPProtectionProfile.EEP_A, dABSubChannel.ProtectionLevel, _MSCViterbi);

            _energyDispersal = new EnergyDispersal();
            _rs = new ReedSolomonCodecControlBlock(8, 0x11D, 0, 1, 10, 135);

            _fragmentSize = Convert.ToInt32(dABSubChannel.Length * CUSize);
            _bitRate = dABSubChannel.Bitrate;

            _interleaveData = new sbyte[16, _fragmentSize];
            _tempX = new sbyte[_fragmentSize];

            _frameLength = 24 * dABSubChannel.Bitrate / 8;
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

        public void ProcessCIFFragmentData(sbyte[] DABBuffer)
        {
            // dab-audio.run

            for (var i = 0; i < _fragmentSize; i++)
            {
                var index = (_interleaverIndex + InterleaveMap[i & 15]) & 15;
                _tempX[i] = _interleaveData[index, i];
                _interleaveData[_interleaverIndex,i] = DABBuffer[i];
            }

            _interleaverIndex = (_interleaverIndex + 1) & 15;

            //  only continue when de-interleaver is filled
            if (_countforInterleaver <= 15)
            {
                _countforInterleaver++;
                return;
            }

            // just for debug
            // this helps to find the same data in welle.io
            //if ((_tempX[0] == 119) && (_tempX[1] == 82))
            //{
            //}

            var outV = _EEPProtection.Deconvolve(_tempX);
            var bytes = _energyDispersal.Dedisperse(outV);

            // -> decoder_adapter.addtoFrame

            var finalBytes = GetFrameBytes(bytes, _bitRate);

            _DABQueue.Enqueue(finalBytes);
        }

        /// <summary>
        /// Convert 8 bits (stored in one uint8) into one uint8
        /// </summary>
        /// <returns></returns>
        private byte[] GetFrameBytes(byte[] v, int bitRate)
        {
            try
            {
                var length = 24 * bitRate / 8; // should be 2880 bytes

                var res = new byte[length];

                for (var i = 0; i < length; i++)
                {
                    res[i] = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        res[i] <<= 1;
                        res[i] |= Convert.ToByte(v[8 * i + j] & 01);
                    }
                }

                return res;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public void Feed(byte[] data)
        {
            // ~ dabplus_decoder.cpp SuperFRameFilter.Feed

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
            var subch_index = SFLength / 120;
            var total_corr_count = 0;
            var uncorr_errors = false;

            // process all RS packets
            for (int i = 0; i < subch_index; i++)
            {
                for (int pos = 0; pos < 120; pos++)
                {
                    _rsPacket[pos] = sf[pos * subch_index + i];
                }

                // detect errors
                int corr_count = decode_rs_char(_rs, _rsPacket, _corrPos, 0);
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
        }

        private int decode_rs_char(ReedSolomonCodecControlBlock rs, byte[] data, int[] eras_pos, int no_eras)
        {
            return - 1;
        }
    }
}
