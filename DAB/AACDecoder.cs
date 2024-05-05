using System;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using LoggerService;

namespace RTLSDR.DAB
{
    public class AACDecoder
    {
#if _WINDOWS
        [DllImport("libfaad", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecOpen();

        [DllImport("libfaad2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint buffer_size, out uint samplerate, out uint channels);

        [DllImport("libfaad2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecDecode(IntPtr hpDecoder, out AACDecFrameInfo hInfo, byte[] buffer, int buffer_size);

        [DllImport("libfaad2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void NeAACDecClose(IntPtr hDecoder);
#else
        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr NeAACDecOpen();

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NeAACDecInit(IntPtr hDecoder, byte[] buffer, uint size, out uint samplerate, out uint channels);

        [DllImport("libfaad.so.2", CallingConvention = CallingConvention.Cdecl)]
        //public static extern void NeAACDecDecode2(IntPtr hDecoder, out AACDecFrameInfo hInfo, byte[] buffer, uint size, out byte[] pcm, uint maxSize);
        public static extern IntPtr NeAACDecDecode(IntPtr hpDecoder, out AACDecFrameInfo hInfo, byte[] buffer, int buffer_size);

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

        public bool Open(AACSuperFrameHeader format)
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

                int result = NeAACDecInit(_hDecoder, asc, (uint)asc_len, out _samplerate, out _channels);

                _loggingService.Debug($"faad2 initialized: samplerate: {_samplerate}, channels: {_channels}");

                if (result != 0)
                {
                    _loggingService.Error(null, "Error initializing faad2");
                    NeAACDecClose(_hDecoder);
                    return false;
                }

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

                AACDecFrameInfo frameInfo = new AACDecFrameInfo();

                var result = NeAACDecDecode(_hDecoder, out frameInfo, aacData, aacData.Length);

                if ((frameInfo.bytesconsumed == aacData.Length) && frameInfo.samples>0)
                {
                    pcmData = new byte[frameInfo.samples * 2];
                    Marshal.Copy(result, pcmData, 0, frameInfo.samples * 2);
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
