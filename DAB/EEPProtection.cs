using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public class EEPProtection
    {
        private int L1 { get; set; }
        private int L2 { get; set; }
        private Viterbi _viterbi = null;

        private Int16[] PI1 { get; set; }
        private Int16[] PI2 { get; set; }

        private Int16[] PI_X = new Int16[24] {
            1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0,
            1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0
        };

        private Int16[,] p_codes = new Int16[24, 32]
        {
            { 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0},// 1
            { 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0},// 2
            { 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0},// 3
            { 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0},// 4
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,0,0,0},// 5
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0},// 6
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,0,0,0},// 7
            { 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 8
            { 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 9
            { 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 10
            { 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,0,0, 1,1,0,0},// 11
            { 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0},// 12
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,0,0},// 13
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0},// 14
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,0,0},// 15
            { 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 16
            { 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 17
            { 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 18
            { 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,0, 1,1,1,0},// 19
            { 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0},// 20
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,0},// 21
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0},// 22
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,0},// 23
            { 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1, 1,1,1,1} // 24
        };


        private Int16[] getPCodes(Int16 x)
        {
            var res = new Int16[32];
            for (var i = 0; i < 32; i++)
            {
                res[i] = p_codes[x, i];
            }

            return res;
        }

        public EEPProtection(int bitRate, EEPProtectionProfile profile, EEPProtectionLevel level, Viterbi viterbi)
        {
            _viterbi = viterbi;

            if (profile == EEPProtectionProfile.EEP_A)
            {
                switch (level)
                {
                    case EEPProtectionLevel.EEP_1:
                        L1 = 6 * bitRate / 8 - 3;
                        L2 = 3;
                        PI1 = getPCodes(24 - 1);
                        PI2 = getPCodes(23 - 1);
                        break;

                    case EEPProtectionLevel.EEP_2:
                        if (bitRate == 8)
                        {
                            L1 = 5;
                            L2 = 1;
                            PI1 = getPCodes(13 - 1);
                            PI2 = getPCodes(12 - 1);
                        }
                        else
                        {
                            L1 = 2 * bitRate / 8 - 3;
                            L2 = 4 * bitRate / 8 + 3;
                            PI1 = getPCodes(14 - 1);
                            PI2 = getPCodes(13 - 1);
                        }
                        break;

                    case EEPProtectionLevel.EEP_3:
                        L1 = 6 * bitRate / 8 - 3;
                        L2 = 3;
                        PI1 = getPCodes(8 - 1);
                        PI2 = getPCodes(7 - 1);
                        break;

                    case EEPProtectionLevel.EEP_4:
                        L1 = 4 * bitRate / 8 - 3;
                        L2 = 2 * bitRate / 8 + 3;
                        PI1 = getPCodes(3 - 1);
                        PI2 = getPCodes(2 - 1);
                        break;

                        //default:
                        //throw std::logic_error("Invalid EEP_A level");
                }
            }
            else
            {
                switch (level)
                {
                    case EEPProtectionLevel.EEP_4:
                        L1 = 24 * bitRate / 32 - 3;
                        L2 = 3;
                        PI1 = getPCodes(2 - 1);
                        PI2 = getPCodes(1 - 1);
                        break;

                    case EEPProtectionLevel.EEP_3:
                        L1 = 24 * bitRate / 32 - 3;
                        L2 = 3;
                        PI1 = getPCodes(4 - 1);
                        PI2 = getPCodes(3 - 1);
                        break;

                    case EEPProtectionLevel.EEP_2:
                        L1 = 24 * bitRate / 32 - 3;
                        L2 = 3;
                        PI1 = getPCodes(6 - 1);
                        PI2 = getPCodes(5 - 1);
                        break;

                    case EEPProtectionLevel.EEP_1:
                        L1 = 24 * bitRate / 32 - 3;
                        L2 = 3;
                        PI1 = getPCodes(10 - 1);
                        PI2 = getPCodes(9 - 1);
                        break;

                        //default:
                        //  throw std::logic_error("Invalid EEP_A level");
                }
            }
        }

        public byte[] Deconvolve(sbyte[] v)
        {
            try
            {
                int i, j;
                int inputCounter = 0;
                int viterbiCounter = 0;
                var outSize = 2880;

                var viterbiBlock = new sbyte[outSize * 4 + 24];

                //  according to the standard we process the logical frame
                //  with a pair of tuples
                //  (L1, PI1), (L2, PI2)
                //
                for (i = 0; i < L1; i++)
                {
                    for (j = 0; j < 128; j++)
                    {
                        if (PI1[j % 32] != 0)
                        {
                            viterbiBlock[viterbiCounter] = v[inputCounter++];
                        }
                        viterbiCounter++;
                    }
                }

                for (i = 0; i < L2; i++)
                {
                    for (j = 0; j < 128; j++)
                    {
                        if (PI2[j % 32] != 0)
                            viterbiBlock[viterbiCounter] = v[inputCounter++];
                        viterbiCounter++;
                    }
                }
                //  we had a final block of 24 bits  with puncturing according to PI_X
                //  This block constitutes the 6 * 4 bits of the register itself.
                for (i = 0; i < 24; i++)
                {
                    if (PI_X[i] != 0)
                        viterbiBlock[viterbiCounter] = v[inputCounter++];
                    viterbiCounter++;
                }

                return _viterbi.Deconvolve(viterbiBlock);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static int GetBitrate(EEPProtectionProfile eepProfile, EEPProtectionLevel eepLevel, int length)
        {
            switch (eepProfile)
            {
                case EEPProtectionProfile.EEP_A:
                    switch (eepLevel)
                    {
                        case EEPProtectionLevel.EEP_1:
                            return length / 12 * 8;
                        case EEPProtectionLevel.EEP_2:
                            return length / 8 * 8;
                        case EEPProtectionLevel.EEP_3:
                            return length / 6 * 8;
                        case EEPProtectionLevel.EEP_4:
                            return length / 4 * 8;
                    }
                    break;
                case EEPProtectionProfile.EEP_B:
                    switch (eepLevel)
                    {
                        case EEPProtectionLevel.EEP_1:
                            return length / 27 * 32;
                        case EEPProtectionLevel.EEP_2:
                            return length / 21 * 32;
                        case EEPProtectionLevel.EEP_3:
                            return length / 18 * 32;
                        case EEPProtectionLevel.EEP_4:
                            return length / 15 * 32;
                    }
                    break;
            }

            throw new Exception("Unsupported EEP protection");
        }
    }
}
