using System;
namespace RTLSDR.DAB
{
    public struct AACSuperFrameFormatDecStruct
    {
        public bool dac_rate;
        public bool sbr_flag;
        public bool aac_channel_mode;
        public bool ps_flag;
        public int mpeg_surround_config;
    }
}
