﻿using LoggerService;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using RTLSDR.Common;

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
        private DABCRC _crc16;
        private StringBitWriter _bitWriter;
        private AACSuperFrameHeader _aacSuperFrameHeader = null;

        private event EventHandler _onAACDataDemodulated;
        private event EventHandler _onAACSuperFrameHeaderDemodulated;
        public event EventHandler OnProcessedSuperFramesChanged = null;

        private ConcurrentQueue<byte[]> _DABQueue;

        public int ProcessedSuperFramesCount { get; set; } = 0;
        public int ProcessedSuperFramesSyncedCount { get; set; } = 0;
        public int ProcessedSuperFramesAUsCount { get; set; } = 0;
        public int ProcessedSuperFramesAUsSyncedCount { get; set; } = 0;

        private bool _synced = false;

        public DABDecoder(ILoggingService loggingService, DABSubChannel dABSubChannel, int CUSize, ConcurrentQueue<byte[]> queue,
            EventHandler OnAACDataDemodulated, EventHandler OnAACSuperFrameHeaderDemodulated)
        {
            _DABQueue = queue;
            _loggingService = loggingService;

            _onAACDataDemodulated = OnAACDataDemodulated;
            _onAACSuperFrameHeaderDemodulated = OnAACSuperFrameHeaderDemodulated;

            _MSCViterbi = new Viterbi(dABSubChannel.Bitrate * 24);
            _EEPProtection = new EEPProtection(dABSubChannel.Bitrate, dABSubChannel.ProtectionProfile, dABSubChannel.ProtectionLevel, _MSCViterbi);

            _energyDispersal = new EnergyDispersal();
            _rs = new ReedSolomonErrorCorrection(8, 0x11D, 0, 1, 10, 135);

            _fragmentSize = Convert.ToInt32(dABSubChannel.Length * CUSize);
            _bitRate = dABSubChannel.Bitrate;

            _interleaveData = new sbyte[16, _fragmentSize];
            _tempX = new sbyte[_fragmentSize];

            _frameLength = 24 * dABSubChannel.Bitrate / 8;
            _buffer = new List<byte>();

            for (var i = 0; i < _rsPacket.Length; i++)
            {
                _rsPacket[i] = 0;
            }
            for (var i = 0; i < _corrPos.Length; i++)
            {
                _corrPos[i] = 0;
            }

            _crcFireCode = new DABCRC(false, false, 0x782F);
            _crc16 = new DABCRC(true, true, 0x1021);

            _bitWriter = new StringBitWriter();
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
                _interleaveData[_interleaverIndex, i] = DABBuffer[i];
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
            if (outV == null)
            {
                return;
            }

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

            return true;
        }

        public void Feed(byte[] data)
        {
            try
            {
                _buffer.AddRange(data);
                _currentFrame++;

                if (_currentFrame < 5)
                {
                    return;
                }
                if (_currentFrame > 5)
                {
                    // drop first part
                    _buffer.RemoveRange(0, data.Length);
                }

                var bytes = _buffer.ToArray();

                ProcessedSuperFramesCount++;

                if (OnProcessedSuperFramesChanged != null)
                {
                    OnProcessedSuperFramesChanged(this, new EventArgs());
                }

                DecodeSuperFrame(bytes);

                if (CheckSync(bytes))
                {
                    _currentFrame = 0;
                    _buffer.Clear();

                    ProcessedSuperFramesSyncedCount++;

                    if (OnProcessedSuperFramesChanged != null)
                    {
                        OnProcessedSuperFramesChanged(this, new EventArgs());
                    }

                    _synced = true;
                    _buffer.Clear();

                    if (_aacSuperFrameHeader == null)
                    {
                        _aacSuperFrameHeader = AACSuperFrameHeader.Parse(bytes);
                        // TODO: check for correct order of start offsets
                    }

                    // decode frames
                    for (int i = 0; i < _aacSuperFrameHeader.NumAUs; i++)
                    {
                        ProcessedSuperFramesAUsCount++;

                        if (OnProcessedSuperFramesChanged != null)
                        {
                            OnProcessedSuperFramesChanged(this, new EventArgs());
                        }

                        var start = _aacSuperFrameHeader.AUStart[i];
                        var finish = i == _aacSuperFrameHeader.NumAUs - 1 ? bytes.Length / 120 * 110 : _aacSuperFrameHeader.AUStart[i + 1];
                        var len = finish - start;

                        // last two bytes hold CRC
                        var crcStored = bytes[finish - 2] << 8 | bytes[finish - 1];

                        var AUData = new byte[len - 2];
                        Buffer.BlockCopy(bytes, start, AUData, 0, len - 2);

                        var crcCalced = _crc16.CalcCRC(AUData);

                        if (crcStored != crcCalced)
                        {
                            //_loggingService.Debug("DABDecoder: crc failed");
                            continue;
                        }

                        ProcessedSuperFramesAUsSyncedCount++;

                        if (OnProcessedSuperFramesChanged != null)
                        {
                            OnProcessedSuperFramesChanged(this, new EventArgs());
                        }

                        // send to _AACQueue
                        if (_onAACSuperFrameHeaderDemodulated != null)
                        {
                            _onAACSuperFrameHeaderDemodulated(this, new AACSeperFrameHaderDemodulatedEventArgs()
                            {
                                Header = _aacSuperFrameHeader
                            });
                        }

                        if (_onAACDataDemodulated != null)
                        {
                            _onAACDataDemodulated(this, new DataDemodulatedEventArgs()
                            {
                                Data = AUData
                            });
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, ex.StackTrace);
            }
        }

        private byte[] GetStreamDataADTS(byte[] data, AACSuperFrameHeader frameHeader)
        {
            var packetLength = data.Length;

            // ADTS header (7 bytes)
            byte[] adtsHeader = new byte[7];
            adtsHeader[0] = 0xFF; // Syncword
            adtsHeader[1] = 0xF9; // MPEG-2 Layer 4, AAC LC
            adtsHeader[2] = 0x40; // Profile (Main), Sampling frequency index (4), Private bit (0), Channel configuration (2)
            adtsHeader[3] = (byte)(((packetLength + 7) & 0x1FFF) >> 5); // Frame length (upper 5 bits)
            adtsHeader[4] = (byte)(((packetLength + 7) & 0x1FFF) << 3); // Frame length (lower 3 bits)
            adtsHeader[4] |= 0x00; // Buffer fullness (0)
            adtsHeader[5] = 0xFC; // Number of AAC frames (1)

            var res = new List<byte>();
            res.AddRange(adtsHeader);
            res.AddRange(data);

            return res.ToArray();
        }

        private byte[] GetStreamDataLATM(byte[] data, AACSuperFrameHeader frameHeader)
        {
            _bitWriter.Clear();

            _bitWriter.AddBits(0x2B7, 11);   // syncword
            _bitWriter.AddBits(0, 13);       // audioMuxLengthBytes - written later

            // AudioMuxElement(1)
            _bitWriter.AddBits(0, 1);        // useSameStreamMux

            // StreamMuxConfig()
            _bitWriter.AddBits(0, 1);        // audioMuxVersion
            _bitWriter.AddBits(1, 1);        // allStreamsSameTimeFraming
            _bitWriter.AddBits(0, 6);        // numSubFrames
            _bitWriter.AddBits(0, 4);        // numProgram
            _bitWriter.AddBits(0, 3);		 // numLayer

            var coreSrIndex = frameHeader.DacRate == DacRateEnum.DacRate48KHz
                ? (frameHeader.SBRFlag == SBRFlagEnum.SBRUsed ? 6 : 3)
                : (frameHeader.SBRFlag == SBRFlagEnum.SBRUsed ? 8 : 5);

            var coreChConfig = frameHeader.AACChannelMode == AACChannelModeEnum.Stereo ? 2 : 1;
            var coreExtensionSrIndex = frameHeader.DacRate == DacRateEnum.DacRate48KHz ? 3 : 5;

            if (frameHeader.SBRFlag == SBRFlagEnum.SBRUsed)
            {
                _bitWriter.AddBits(0b00101, 5);                         // SBR
                _bitWriter.AddBits(coreSrIndex, 4);      // samplingFrequencyIndex
                _bitWriter.AddBits(coreChConfig, 4);     // channelConfiguration
                _bitWriter.AddBits(coreExtensionSrIndex, 4); // extensionSamplingFrequencyIndex
                _bitWriter.AddBits(0b00010, 5);                         // AAC LC
                _bitWriter.AddBits(0b100, 3);                           // GASpecificConfig() with 960 transform
            }
            else
            {
                _bitWriter.AddBits(0b00010, 5);                         // AAC LC
                _bitWriter.AddBits(coreSrIndex, 4);      // samplingFrequencyIndex
                _bitWriter.AddBits(coreChConfig, 4);     // channelConfiguration
                _bitWriter.AddBits(0b100, 3);                           // GASpecificConfig() with 960 transform
            }

            _bitWriter.AddBits(0b000, 3);    // frameLengthType
            _bitWriter.AddBits(0xFF, 8);     // latmBufferFullness
            _bitWriter.AddBits(0, 1);        // otherDataPresent
            _bitWriter.AddBits(0, 1);        // crcCheckPresent

            // PayloadLengthInfo()
            for (var i = 0; i < Convert.ToInt32(Math.Truncate(data.Length / 255.0)); i++)
            {
                _bitWriter.AddBits(0xFF, 8);
            }
            _bitWriter.AddBits(data.Length % 255, 8);

            var streamBytes = _bitWriter.GetBytes();
            streamBytes.AddRange(data);

            var len = streamBytes.Count - 3;
            streamBytes[1] |= Convert.ToByte((len >> 8) & 0x1F);
            streamBytes[2] = Convert.ToByte(len & 0xFF);

            return streamBytes.ToArray();
        }

        private bool DecodeSuperFrame(byte[] sf)
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

            return uncorr_errors;
        }
    }
}
