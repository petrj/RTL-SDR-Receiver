using System;
namespace RTLSDR.DAB
{
    /// <summary>
    /// Table 3: Definition of dac_rate
    /// </summary>
    public enum DacRateEnum
    {
        DacRate32KHz = 0,
        DacRate48KHz = 1
    }

    /// <summary>
    /// Table 4: Definition of sbr_flag
    /// </summary>
    public enum SBRFlagEnum
    {
        SBRNotUsed = 0,
        SBRUsed = 1
    }

    /// <summary>
    /// Table 5: Definition of aac_channel_mode
    /// </summary>
    public enum AACChannelModeEnum
    {
        Mono = 0,
        Stereo = 1
    }

    /// <summary>
    /// Table 6: Definition of ps_flag (Parametric stereo)
    /// </summary>
    public enum PSFlagEnum
    {
        PSNotUsed = 0,
        PSUsed = 1
    }

    /// <summary>
    /// Table 7: Definition of mpeg_surround_config
    /// </summary>
    public enum MPEGSurroundEnum
    {
        MPEGSurroundNotUsed = 0,
        MPEGSurround51 = 1,
        MPEGSurround71 = 2,
        Other = 7
    }

    /// <summary>
    /// https://www.etsi.org/deliver/etsi_ts/102500_102599/102563/02.01.01_60/ts_102563v020101p.pdf
    /// ETSI TS 102 563 V2.1.1 (2017-01)10 Table 2
    /// </summary>
    public class AACSuperFrame
    {
        public int FireCode { get; set; }

        public DacRateEnum DacRate { get; set; }
        public SBRFlagEnum SBRFlag { get; set; }
        public AACChannelModeEnum AACChannelMode { get; set; }
        public PSFlagEnum PSFlag { get; set; }
        public MPEGSurroundEnum MPEGSurround { get; set; }

        public static AACSuperFrame Parse(byte[] data)
        {
            var superFrame = new AACSuperFrame();

            superFrame.FireCode = (data[0] << 8) | data[1];
            superFrame.DacRate = (DacRateEnum)((data[2] & 64) >> 6);
            superFrame.SBRFlag = (SBRFlagEnum)((data[2] & 32) >> 5);
            superFrame.AACChannelMode = (AACChannelModeEnum)((data[2] & 16) >> 4);
            superFrame.PSFlag = (PSFlagEnum)((data[2] & 8) >> 3);
            superFrame.MPEGSurround = (MPEGSurroundEnum)(data[2] & 7);

            return superFrame;
        }
    }
}
