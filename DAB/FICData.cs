using System;
using LoggerService;

namespace DAB
{
    public class FICData
    {
        private int index { get; set; } = 0;
        private int ficno { get; set; } = 0;

        private const int bitsperBlock = 2 * 1536;
        public const int FICSize = 2034;

        private byte[] Buffer { get; set; } = new byte[FICSize];

        private ILoggingService _loggingService;

        public FICData(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public void Parse(byte[] ficData, int blkno)
        {
            _loggingService.Debug($"Parsing FIC data");

            if (blkno == 1)
            {
                index = 0;
                ficno = 0;
            }

            if ((1 <= blkno) && (blkno <= 3))
            {
                for (int i = 0; i < bitsperBlock; i++)
                {
                    Buffer[index++] = ficData[i];
                    if (index >= FICSize)
                    {
                        //processFicInput(ofdm_input.data(), ficno);
                        index = 0;
                        ficno++;
                    }
                }
            }
            else
            {
               throw new ArgumentException("Invalid ficBlock blkNo\n");
            }
        }
    }
}
