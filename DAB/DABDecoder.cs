﻿using LoggerService;
using RTLSDR.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;

namespace RTLSDR.DAB
{
    public class DABDecoder
    {
        private ILoggingService _loggingService;

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

        private ReedSolomonErrorCorrection _rs;
        private DABCRC _crcFireCode;

        private ConcurrentQueue<byte[]> _DABQueue;

        private bool _synced = false;

        public DABDecoder(ILoggingService loggingService, DABSubChannel dABSubChannel, int CUSize, ConcurrentQueue<byte[]> queue)
        {
            _DABQueue = queue;
            _loggingService = loggingService;

            _MSCViterbi = new Viterbi(dABSubChannel.Bitrate*24);
            _EEPProtection = new EEPProtection(dABSubChannel.Bitrate, EEPProtectionProfile.EEP_A, dABSubChannel.ProtectionLevel, _MSCViterbi);

            _energyDispersal = new EnergyDispersal();
            _rs = new ReedSolomonErrorCorrection(8, 0x11D, 0, 1, 10, 135);

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

            _crcFireCode = new DABCRC(false, false, 0x782F);
        }

        public bool Synced
        {
            get
            {
                return _synced;
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

        private bool CheckSync(byte[] sf)
        {
            // abort, if au_start is kind of zero (prevent sync on complete zero array)
            if (sf[3] == 0x00 && sf[4] == 0x00)
                return false;

            // try to sync on fire code
            uint crc_stored = Convert.ToUInt16(sf[0] << 8 | sf[1]);

            byte[] dataForCRC = new byte[9];
            Buffer.BlockCopy(sf, 2, dataForCRC, 0, 9);

            uint crc_calced = _crcFireCode.CalcCRC(dataForCRC);
            if (crc_stored != crc_calced)
                return false;

            /*

            // handle format
            sf_format.dac_rate = sf[2] & 0x40;
            sf_format.sbr_flag = sf[2] & 0x20;
            sf_format.aac_channel_mode = sf[2] & 0x10;
            sf_format.ps_flag = sf[2] & 0x08;
            sf_format.mpeg_surround_config = sf[2] & 0x07;


            // determine number/start of AUs
            num_aus = sf_format.dac_rate ? (sf_format.sbr_flag ? 3 : 6) : (sf_format.sbr_flag ? 2 : 4);

            au_start[0] = sf_format.dac_rate ? (sf_format.sbr_flag ? 6 : 11) : (sf_format.sbr_flag ? 5 : 8);
            au_start[num_aus] = sf_len / 120 * 110; // pseudo-next AU (w/o RS coding)

            au_start[1] = sf[3] << 4 | sf[4] >> 4;
            if (num_aus >= 3)
                au_start[2] = (sf[4] & 0x0F) << 8 | sf[5];
            if (num_aus >= 4)
                au_start[3] = sf[6] << 4 | sf[7] >> 4;
            if (num_aus == 6)
            {
                au_start[4] = (sf[7] & 0x0F) << 8 | sf[8];
                au_start[5] = sf[9] << 4 | sf[10] >> 4;
            }

            // simple plausi check for correct order of start offsets
            for (int i = 0; i < num_aus; i++)
                if (au_start[i] >= au_start[i + 1])
                    return false;

                    
            */
            return true;
        }


        public void Feed(byte[] data)
        {
            // ~ dabplus_decoder.cpp SuperFRameFilter.Feed

            _buffer.AddRange(data);

            _currentFrame++;

            if (_currentFrame == 5)
            {
                var bytes = _buffer.ToArray();

                DecodeSuperFrame(bytes);

                if (CheckSync(bytes))
                {
                    _synced = true;
                    _buffer.Clear();

                    // TODO: decode frames

                    _currentFrame = 0;
                    //_loggingService.Debug("SuperFrame synced");
                } else
                {
                    // drop first part
                    _buffer.RemoveRange(0, data.Length);
                    _synced = false;
                    _currentFrame--;
                    // not synced
                }
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
                int corr_count = _rs.DecodeRSChar(_rsPacket, _corrPos, 0);
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
    }
}
