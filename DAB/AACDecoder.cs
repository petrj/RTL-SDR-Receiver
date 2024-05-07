using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using LoggerService;

namespace RTLSDR.DAB
{
    public class AACDecoder
    {
#if _WINDOWS
        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecOpen();

        //[DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern uint NeAACDecGetCapabilities();

        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecGetCurrentConfiguration(IntPtr hDecoder);

        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecSetConfiguration(IntPtr hDecoder, IntPtr config);

        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint buffer_size, out uint samplerate, out uint channels);

        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit2(IntPtr hDecoder, byte[] buffer, uint size, out uint samplerate, out uint channels);

        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecDecode(IntPtr hpDecoder, out AACDecFrameInfo hInfo, byte[] buffer, int buffer_size);

        [DllImport("libfaad2.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NeAACDecClose(IntPtr hDecoder);
#else
        //[DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        //public static extern uint NeAACDecGetCapabilities();

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecGetCurrentConfiguration(IntPtr hDecoder);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecSetConfiguration(IntPtr hDecoder, IntPtr config);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecOpen();

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit2(IntPtr hDecoder, byte[] buffer, uint size, out uint samplerate, out uint channels);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecDecode(IntPtr hpDecoder, ref AACDecFrameInfo hInfo, byte[] buffer, int buffer_size);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NeAACDecDecode2(IntPtr hDecoder, out AACDecFrameInfo hInfo, byte[] buffer, uint size, out byte[] pcm, uint maxSize);

        [DllImport("libfaad.so.2")]
        public static extern void NeAACDecClose(IntPtr hDecoder);
#endif

        private IntPtr _hDecoder = IntPtr.Zero;
        uint _samplerate;
        uint _channels;
        private ILoggingService _loggingService;

        public AACDecoder(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public bool Init(AACSuperFrameHeader format)
        {
            try
            {
                _loggingService.Debug("Initializing faad2");

                /* Does not implemented on windows

                var cap = NeAACDecGetCapabilities();
                if (!((cap & 1) == 1))
                {
                    _loggingService.Error(null, "AACDecoder: no LC decoding support");
                    return false;
                }
                */

                _hDecoder = NeAACDecOpen();
                if (_hDecoder == IntPtr.Zero)
                {
                    _loggingService.Error(null, "Error initializing faad2");
                    return false;
                }

                // set general config
                var configPtr = NeAACDecGetCurrentConfiguration(_hDecoder);

                var config = (AACDecConfiguration) Marshal.PtrToStructure(configPtr, typeof(AACDecConfiguration));
                config.defObjectType = 1;
                config.defSampleRate = 44100;
                config.dontUpSampleImplicitSBR = 0;
                config.downMatrix = 0;
                config.outputFormat = 1; // FAAD_FMT_16BIT
                config.useOldADTSFormat = 0;

                Marshal.StructureToPtr(config, configPtr, false);

                var setConfigRes = NeAACDecSetConfiguration(_hDecoder, configPtr);
                if (setConfigRes != 1)
                {
                    _loggingService.Error(null, "Error initializing faad2");
                }

                var asc_len = 0;
                var asc = new byte[7];

                var coreSrIndex = format.DacRate == DacRateEnum.DacRate48KHz ? (format.SBRFlag == SBRFlagEnum.SBRUsed ? 6 : 3) : (format.SBRFlag == SBRFlagEnum.SBRUsed ? 8 : 5);  // 24/48/16/32 kHz
                var coreChConfig = format.AACChannelMode == AACChannelModeEnum.Stereo ? 2 : 1;
                var extensionSrIndex = format.DacRate == DacRateEnum.DacRate48KHz ? 3 : 5;    // 48/32 kHz

                asc[asc_len++] = Convert.ToByte(0b00010 << 3 | coreSrIndex >> 1);
                asc[asc_len++] = Convert.ToByte((coreSrIndex & 0x01) << 7 | coreChConfig << 3 | 0b100);

                if (format.SBRFlag == SBRFlagEnum.SBRUsed)
                {
                    // add SBR
                    asc[asc_len++] = 0x56;
                    asc[asc_len++] = 0xE5;
                    asc[asc_len++] = Convert.ToByte(0x80 | (extensionSrIndex << 3));

                    if (format.PSFlag == PSFlagEnum.PSUsed)
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
            } catch (Exception ex)
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

                var frameInfo = new AACDecFrameInfo();

                var resultPtr = NeAACDecDecode(_hDecoder, out frameInfo, aacData, aacData.Length);

                if ((frameInfo.bytesconsumed == 0) && frameInfo.samples == 0)
                {
                    return null; // no data
                }

                if (frameInfo.bytesconsumed != aacData.Length)
                {
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
            // Uzavření dekodéru
            NeAACDecClose(_hDecoder);

            Console.WriteLine("Dekódování dokončeno.");
        }
    }

}
