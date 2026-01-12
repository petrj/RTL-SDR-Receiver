using LoggerService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class AACDecoderLinux : IAACDecoder
    {
        public const string libPath = "libfaad.so.2";

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NeAACDecOpen();

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern void NeAACDecClose(IntPtr hDecoder);

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NeAACDecGetCurrentConfiguration(IntPtr hDecoder);

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NeAACDecSetConfiguration(IntPtr hDecoder, IntPtr config);

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint buffer_size, out uint samplerate, out uint channels);

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern int NeAACDecInit2(IntPtr hDecoder, byte[] buffer, uint size, out ulong samplerate, out ulong channels);

        [DllImport(libPath, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NeAACDecDecode(IntPtr hpDecoder, out AACDecFrameInfoLinux hInfo, byte[] buffer, ulong buffer_size);

        private IntPtr _hDecoder = IntPtr.Zero;
        private ulong _samplerate;
        private ulong _channels;
        private ILoggingService _loggingService;

        public AACDecoderLinux(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public bool Init(AACSuperFrameHeader format)
        {
            return Init(format.SBRFlag, format.DacRate, format.AACChannelMode, format.PSFlag);
        }

        protected bool Init(SBRFlagEnum SBRFlag, DacRateEnum dacRate, AACChannelModeEnum channelMode, PSFlagEnum PSFlag)
        {
            try
            {
                _loggingService.Debug("Initializing faad2");

                _hDecoder = NeAACDecOpen();
                if (_hDecoder == IntPtr.Zero)
                {
                    _loggingService.Error(null, "Error initializing faad2");
                    return false;
                }

                // set general config
                var configPtr = NeAACDecGetCurrentConfiguration(_hDecoder);


                var config = (AACDecConfiguration)Marshal.PtrToStructure(configPtr, typeof(AACDecConfiguration));
                config.dontUpSampleImplicitSBR = 0;
                config.outputFormat = 1; // FAAD_FMT_16BIT

                Marshal.StructureToPtr(config, configPtr, false);

                var setConfigRes = NeAACDecSetConfiguration(_hDecoder, configPtr);
                if (setConfigRes != 1)
                {
                    _loggingService.Error(null, "Error initializing faad2");
                }

                var asc_len = 0;
                var asc = new byte[7];

                var coreSrIndex = dacRate == DacRateEnum.DacRate48KHz ? (SBRFlag == SBRFlagEnum.SBRUsed ? 6 : 3) : (SBRFlag == SBRFlagEnum.SBRUsed ? 8 : 5);  // 24/48/16/32 kHz
                var coreChConfig = channelMode == AACChannelModeEnum.Stereo ? 2 : 1;
                var extensionSrIndex = dacRate == DacRateEnum.DacRate48KHz ? 3 : 5;    // 48/32 kHz

                asc[asc_len++] = Convert.ToByte(0b00010 << 3 | coreSrIndex >> 1);
                asc[asc_len++] = Convert.ToByte((coreSrIndex & 0x01) << 7 | coreChConfig << 3 | 0b100);

                if (SBRFlag == SBRFlagEnum.SBRUsed)
                {
                    // add SBR
                    asc[asc_len++] = 0x56;
                    asc[asc_len++] = 0xE5;
                    asc[asc_len++] = Convert.ToByte(0x80 | (extensionSrIndex << 3));

                    if (PSFlag == PSFlagEnum.PSUsed)
                    {
                        // add PS
                        asc[asc_len - 1] |= 0x05;
                        asc[asc_len++] = 0x48;
                        asc[asc_len++] = 0x80;
                    }
                }

                int result = NeAACDecInit2(_hDecoder, asc, (uint)asc_len, out _samplerate, out _channels);

                if (result != 0)
                {
                    _loggingService.Error(null, "Error initializing faad2");
                    NeAACDecClose(_hDecoder);
                    return false;
                }

                _loggingService.Debug($"faad2 initialized: samplerate: {_samplerate}, channels: {_channels}");

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, "Error initializing faad2");
                return false;
            }
        }

        public byte[] DecodeAAC(byte[] aacData)
        {
            try
            {
                byte[] pcmData = null;

                var frameInfo = new AACDecFrameInfoLinux();
                IntPtr frameInfoPtr = IntPtr.Zero;

                var resultPtr = NeAACDecDecode(_hDecoder, out frameInfo, aacData, (ulong)aacData.Length);

                if (Convert.ToInt32(frameInfo.bytesconsumed) != aacData.Length)
                {
                    _loggingService.Info($"DecodeAAC failed : only {frameInfo.bytesconsumed}/{aacData.Length} bytes processed");
                    return null; // consumed only part
                }

                if (frameInfo.samples > 0)
                {
                    pcmData = new byte[frameInfo.samples * 2];
                    Marshal.Copy(resultPtr, pcmData, 0, pcmData.Length);
                }

                return pcmData;
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex, "DecodeAAC failed");
                return null;
            }
        }

        public void Close()
        {            
            NeAACDecClose(_hDecoder);
        }
    }
}
