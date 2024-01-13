using System;
using System.Collections.Generic;
using LoggerService;

namespace DAB
{
    /*
        Free .NET DAB+ library

        -   based upon welle.io (https://github.com/AlbrechtL/welle.io)
        -   DAB documentation: https://www.etsi.org/deliver/etsi_en/300400_300499/300401/02.01.01_60/en_300401v020101p.pdf
    */

    public class FICData
    {
        private sbyte[] _buffer { get; set; } = new sbyte[FICSize];
        private int _index { get; set; } = 0;
        private int _ficno { get; set; } = 0;

        private const int BitsperBlock = 2 * 1536;
        private const int FICSize = 2304;
        private const int RATE = 4;
        private const int NUMSTATES = 64;
        private const int K = 7;
        private const int FrameBits = 768;
        private const int METRICSHIFT = 0;
        private const int PRECISIONSHIFT = 0;
        private const int RENORMALIZE_THRESHOLD = 137;
        private const int ADDSHIFT = (8 - (K - 1));

        private static int[] _maskTable = { 128, 64, 32, 16, 8, 4, 2, 1 };
        private short[] _PI_15;
        private short[] _PI_16;
        private int[] _branchTab;
        private byte[] _PRBS;

        private ILoggingService _loggingService;

        private List<DABService> _DABServices = new List<DABService> ();

        private FIB _fib = null;

        public FICData(ILoggingService loggingService)
        {
            _loggingService = loggingService;

            _fib = new FIB(_loggingService);

            _fib.ProgrammeServiceLabelFound += _fib_ProgramServiceLabelFound;
            _fib.EnsembleFound += _fib_EnsembleFound;
            _fib.SubChannelFound += _fib_SubChannelFound;
            _fib.ServiceComponentFound += _fib_ServiceComponentFound;
            _fib.ServiceComponentGlobalDefinitionFound += _fib_ServiceComponentGlobalDefinitionFound;

            _PI_15 = GetPCodes(15 - 1);
            _PI_16 = GetPCodes(16 - 1);

            var polys = new int[4] { 109, 79, 83, 109 };

            _branchTab = new int[NUMSTATES / 2 * RATE];

            for (int state = 0; state < NUMSTATES / 2; state++)
            {
                for (int i = 0; i < RATE; i++)
                {
                        _branchTab[i * NUMSTATES / 2 + state] =
                        Convert.ToInt32(
                        (polys[i] < 0) ^ Parity((2 * state) & Math.Abs(polys[i])) ? 255 : 0);
                }
            }

            _PRBS = new byte[FrameBits];

            var shiftRegister = new byte[9];
            for (int i = 0; i < 9; i++)
            {
                shiftRegister[i] = 1;
            }

            for (int i = 0; i < 768; i++)
            {
                _PRBS[i] = Convert.ToByte(shiftRegister[8] ^ shiftRegister[4]);
                for (int j = 8; j > 0; j--)
                {
                    shiftRegister[j] = shiftRegister[j - 1];
                }

                shiftRegister[0] = _PRBS[i];
            }
        }

        public List<DABService> Services
        {
            get
            {
                return _DABServices;
            }
        }

        private DABService GetServiceByNumber(uint serviceNumber)
        {
            foreach (var service in _DABServices)
            {
                if (service.ServiceNumber == serviceNumber)
                {
                    return service;
                }
            }
            return null;
        }

        private DABService GetServiceByIdentifier(int serviceIdentifier)
        {
            foreach (var service in _DABServices)
            {
                if (service.ServiceIdentifier == serviceIdentifier)
                {
                    return service;
                }
            }
            return null;
        }

        private DABService GetServiceBySubChId(uint subChId)
        {
            foreach (var service in _DABServices)
            {
                var c = service.GetComponentBySubChId(subChId);
                if (c != null)
                {
                    return service;
                }
            }
            return null;
        }

        private void _fib_ServiceComponentGlobalDefinitionFound(object sender, EventArgs e)
        {
            if (e is ServiceComponentGlobalDefinitionFoundEventArgs gde)
            {
                var service = GetServiceBySubChId(gde.ServiceGlobalDefinition.SubChId);
                if (service != null)
                {
                    if (service.ServiceIdentifier == -1)
                    {
                        service.ServiceIdentifier = Convert.ToInt32(gde.ServiceGlobalDefinition.ServiceIdentifier);
                        _loggingService.Info($"Setting ServiceIdentifier:{Environment.NewLine}{gde.ServiceGlobalDefinition}{Environment.NewLine}{service}");
                    }
                }
            }
        }

        private void _fib_EnsembleFound(object sender, EventArgs e)
        {
            if (e is EnsembleFoundEventArgs ensembleArgs)
            {
            }
        }

        private void _fib_ProgramServiceLabelFound(object sender, EventArgs e)
        {
            if (e is ProgrammeServiceLabelFoundEventArgs sla)
            {
                var service = GetServiceByIdentifier(sla.ProgrammeServiceLabel.ServiceIdentifier);

                if (service != null && service.ServiceName == null)
                {
                    service.ServiceName = sla.ProgrammeServiceLabel.ServiceLabel;
                    _loggingService.Info($"Setting service label:{Environment.NewLine}{sla.ProgrammeServiceLabel}{Environment.NewLine}{service}");
                }
            }
        }

        private void _fib_ServiceComponentFound(object sender, EventArgs e)
        {
            if (e is ServiceComponentFoundEventArgs serviceArgs)
            {
                var service = GetServiceByNumber(serviceArgs.ServiceComponent.ServiceNumber);
                if (service == null)
                {
                    // adding service
                    _loggingService.Info($"Adding Service:{Environment.NewLine}{serviceArgs.ServiceComponent.ToString()}");
                    _DABServices.Add(new DABService()
                    {
                        ServiceNumber = serviceArgs.ServiceComponent.ServiceNumber,
                        Components = serviceArgs.ServiceComponent.Components,
                        CountryId = serviceArgs.ServiceComponent.CountryId,
                        ExtendedCountryCode = serviceArgs.ServiceComponent.ExtendedCountryCode,
                    });
                }
            }
        }

        private void _fib_SubChannelFound(object sender, EventArgs e)
        {
            if (e is SubChannelFoundEventArgs s)
            {
                var service = GetServiceBySubChId(s.SubChannel.SubChId);
                if (service != null)
                {
                    var component = service.GetComponentBySubChId(s.SubChannel.SubChId);
                    if (component.SubChannel == null)
                    {
                        component.SubChannel = s.SubChannel;
                        _loggingService.Info($"Setting subchannel:{Environment.NewLine}{s.SubChannel}{Environment.NewLine}{service}");
                    }
                }
            }
        }

        private bool Parity(int x)
        {
            /* Fold down to one byte */
            x ^= (x >> 16);
            x ^= (x >> 8);
            return Convert.ToBoolean(ParTab[x]);
        }

        private short[] ParTab = new short[16*16]
            { 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              1, 0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1,
              0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0};

        private short[,] PCodes = new short[24, 32] {
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

        private short[] PI_X = new short[24] {
                1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0,
                1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0
            };

        private short[] GetPCodes(int x)
        {
            var res = new List<short>();
            for (int i=0;i<32;i++)
            {
                res.Add(PCodes[x, i]);
            }
            return res.ToArray();
        }

        public void Parse(sbyte[] ficData, int blkno)
        {
            //_loggingService.Debug($"Parsing FIC data");

            if (blkno == 1)
            {
                _index = 0;
                _ficno = 0;
            }

            if ((1 <= blkno) && (blkno <= 3))
            {
                for (int i = 0; i < BitsperBlock; i++)
                {
                    _buffer[_index++] = ficData[i];
                    if (_index >= FICSize)
                    {
                        ProcessFICInput(_buffer, _ficno);
                        _index = 0;
                        _ficno++;
                    }
                }
            }
            else
            {
               throw new ArgumentException("Invalid ficBlock blkNo\n");
            }
        }

        private void ProcessFICInput(sbyte[] data, int ficNo)
        {
            try
            {
                var viterbiBlock = new sbyte[3072 + 24];
                var local = 0;
                int input_counter = 0;

                for (int i = 0; i < 21; i++)
                {
                    for (int k = 0; k < 32 * 4; k++)
                    {
                        if (_PI_16[k % 32] != 0)
                        {
                           viterbiBlock[local] = data[input_counter++];
                        }
                        local++;
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    for (int k = 0; k < 32 * 4; k++)
                    {
                        if (_PI_15[k % 32] != 0)
                        {
                            viterbiBlock[local] = data[input_counter++];
                        }
                        local++;
                    }
                }

                for (int k = 0; k < 24; k++)
                {
                    if (PI_X[k] != 0)
                    {
                        viterbiBlock[local] = data[input_counter++];
                    }
                    local++;
                }

                var bitBuffer_out = Deconvolve(viterbiBlock);

                for (var i=0;i<FrameBits;i++)
                {
                    bitBuffer_out[i] ^= _PRBS[i];
                }

                for (var i = _ficno * 3; i < _ficno * 3 + 3; i++)
                {
                    var ficPartBuffer = new List<byte>();
                    for (var j=0;j<256;j++)
                    {
                        ficPartBuffer.Add(bitBuffer_out[(i % 3) * 256 + j]);
                    }

                    var crcvalid = CheckCRC(ficPartBuffer.ToArray());

                    if (crcvalid)
                    {
                        _fib.Parse(ficPartBuffer.ToArray(), _ficno);
                    } else
                    {
                        //_loggingService.Info("BAD FIC CRC");
                    }

                }

            } catch (Exception ex)
            {
                _loggingService.Error(ex);
            }
        }

        public static bool CheckCRC(byte[] data)
        {
            var size = data.Length;
            var crcPolynome = new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 }; // MSB .. LSB
            byte[] b = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                b[i] = 1;
            }

            for (int i = 0; i < size; i++)
            {
                var d = data[i];
                if (i >= size - 16)
                {
                    d ^= 1;
                }

                if ((b[0] ^ d) == 1)
                {
                    for (int f = 0; f < 15; f++)
                    {
                        b[f] = Convert.ToByte(crcPolynome[f] ^ b[f + 1]);
                    }
                    b[15] = 1;
                }
                else
                {
                    //memmove(&b[0], &b[1], sizeof(uint8_t) * 15); // Shift
                    for (int j=0;j<15;j++)
                    {
                        b[j] = b[j + 1];
                    }
                    b[15] = 0;
                }
            }

            uint crc = 0;
            for (int i = 0; i < 16; i++)
            {
                crc |= Convert.ToUInt32(b[i] << i);
            }
            return crc == 0;
        }

        private byte[] Deconvolve(sbyte[] viterbiBlock)
        {
            var symbols = new int[RATE * (FrameBits + (K - 1))];

            for (int i = 0; i < (FrameBits + (K - 1)) * RATE; i++)
            {
                var temp = (int)viterbiBlock[i] + 127;
                if (temp < 0) temp = 0;
                if (temp > 255) temp = 255;
                symbols[i] = temp;
            }

            var stateInfo = new ViterbiStateInfo(NUMSTATES);

            //  update_viterbi_blk_GENERIC (&vp, symbols, frameBits + (K - 1));

            var nbits = FrameBits + (K - 1);

            stateInfo.Decisions.Clear();

            for (var i=0; i< nbits; i++)
            {
                stateInfo.Decisions.Add(new ViterbiDecision());
            }

            stateInfo.SetCurrentDecisionIndex(0);

            for (int s = 0; s < FrameBits + (K - 1); s++)
            {
                for (int i = 0; i < NUMSTATES / 2; i++)
                {
                    BFLY(i, s, symbols, stateInfo);
                }

                Renormalize(stateInfo.NewMetrics.t, RENORMALIZE_THRESHOLD);

                stateInfo.Swap();
            }

            var data = new byte[(FrameBits + (K - 1)) / 8 + 1];

            // Viterbi::chainback_viterbi(

            var endstate = 0;

            nbits = FrameBits;

            while (nbits-- != 0)
            {
                int k;

                var a = Convert.ToInt64(( stateInfo.Decisions[nbits + (K - 1)].w[(endstate >> ADDSHIFT) / 32]));
                var b = ((endstate >> ADDSHIFT) % 32);

                k = Convert.ToInt32((a >>  b) & 1);

                endstate = (endstate >> 1) | (k << (K - 2 + ADDSHIFT));
                data[nbits >> 3] = Convert.ToByte(endstate);
            }

            var output = new List<byte>();

            for (int i = 0; i < FrameBits; i++)
            {
                output.Add(Getbit(data[i >> 3], i & 07));
            }

            return output.ToArray();
        }

        private byte Getbit(byte v, int o)
        {
            var x = v & _maskTable[o];
            if ((v & _maskTable[o]) == _maskTable[o])
            {
                return 1;
            }
            return 0;
        }

        private void Renormalize(uint[] X, int threshold)
        {
            int i;

            if (X[0] > threshold)
            {
                var min = X[0];
                for (i = 0; i < NUMSTATES; i++)
                {
                    if (min > X[i])
                        min = X[i];
                }

                for (i = 0; i < NUMSTATES; i++)
                {
                    X[i] -= min;
                }
            }
        }

        private void BFLY(
                int i,
                int s,
                int[] syms,
                ViterbiStateInfo vp)
        {
            try
            {
                int j, metric;
                long m0, m1, m2, m3, decision0, decision1;

                metric = 0;
                for (j = 0; j < RATE; j++)
                {
                    metric += (_branchTab[i + j * NUMSTATES / 2] ^ syms[s * RATE + j]) >> METRICSHIFT;
                }

                metric = metric >> PRECISIONSHIFT;
                var max = ((RATE * ((256 - 1) >> METRICSHIFT)) >> PRECISIONSHIFT);

                m0 = vp.OldMetrics.t[i] + metric;
                m1 = vp.OldMetrics.t[i + NUMSTATES / 2] + (max - metric);
                m2 = vp.OldMetrics.t[i] + (max - metric);
                m3 = vp.OldMetrics.t[i + NUMSTATES / 2] + metric;

                decision0 = m0 - m1 > 0 ? 1 : 0;
                decision1 = m2 - m3 > 0 ? 1 : 0;

                vp.NewMetrics.t[2 * i] = decision0 == 1 ? Convert.ToUInt32(m1) : Convert.ToUInt32(m0);
                vp.NewMetrics.t[2 * i + 1] = decision1 == 1 ? Convert.ToUInt32(m3) : Convert.ToUInt32(m2);

                var arg = (decision0 | decision1 << 1) << ((2 * i) & 32 - 1);

                var w = i < 16 ? 0 : 1;
                vp.Decisions[s].w[w] |= Convert.ToUInt32(arg);

                var intStop = 0;
            }
            catch (Exception ex)
            {

            }
        }
    }
}
