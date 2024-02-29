using RTLSDR.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
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

        // decode_rs.h
        private int decode_rs_char(ReedSolomonCodecControlBlock rs, byte[] data, int[] eras_pos, int no_eras)
        {
            int deg_lambda, el, deg_omega;
            int i, j, r, k;
            byte u, q, tmp, num1, num2, den, discr_r;

            var lambda = new byte[rs.nroots + 1];
            var s = new byte[rs.nroots ];  /* Err+Eras Locator poly * and syndrome poly */

            var b = new byte[rs.nroots + 1];
            var t = new byte[rs.nroots + 1];
            var omega = new byte[rs.nroots + 1];

            var root = new byte[rs.nroots];
            var reg = new byte[rs.nroots+1];
            var loc = new byte[rs.nroots];

            int syn_error, count;

            /* form the syndromes; i.e., evaluate data(x) at roots of g(x) */
            for (i = 0; i < rs.nroots; i++)
            {
                s[i] = data[0];
            }

            for (j = 1; j < rs.nn - rs.pad; j++)
            {
                for (i = 0; i < rs.nroots; i++)
                {
                    if (s[i] == 0)
                    {
                        s[i] = data[j];
                    }
                    else
                    {
                        s[i] = Convert.ToByte(data[j] ^ rs.alpha_to[rs.modnn(rs.index_of[s[i]] + (rs.fcr + i) * rs.prim)]);
                    }
                }
            }

            /* Convert syndromes to index form, checking for nonzero condition */
            syn_error = 0;
            for (i = 0; i <rs.nroots; i++)
            {
                syn_error |= s[i];
                s[i] = rs.index_of[s[i]];
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
            for (var l=1;l<lambda.Length;l++)
                lambda[l] = 0;

            if (no_eras > 0)
            {
                /* Init lambda to be the erasure locator polynomial */
                lambda[1] = rs.alpha_to[rs.modnn(rs.prim * (rs.nn - 1 - eras_pos[0]))];
                for (i = 1; i < no_eras; i++)
                {
                    u = rs.modnn(rs.prim * (rs.nn - 1 - eras_pos[i]));
                    for (j = i + 1; j > 0; j--)
                    {
                        tmp = rs.index_of[lambda[j - 1]];
                        if (tmp != rs.nn)
                            lambda[j] ^= rs.alpha_to[rs.modnn(u + tmp)];
                    }
                }
            }

            for (i = 0; i < rs.nroots + 1; i++)
            {
                b[i] = rs.index_of[lambda[i]];
            }

            return - 1;
        }
    }
}
