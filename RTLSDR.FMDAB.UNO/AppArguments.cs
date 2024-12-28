using RTLSDR.DAB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.FMDAB.UNO
{
    public static class AppArguments
    {
        public static int Frequency { get; set; }
        public static int ServiceNumber { get; set; }

        public static void SetFrequencyAndService(string[] args)
        {
            // default frequency => first from DABConstants.DABFrequenciesMHz
            Frequency = Convert.ToInt32(DABConstants.DABFrequenciesMHz.Keys.First() * 1E+6);

            int freq = 0;
            if (args.Length >= 1)
            {
                if (int.TryParse(args[0], out freq))
                {
                    Frequency = freq;
                } else
                {
                    foreach (var freqConstants in DABConstants.DABFrequenciesMHz)
                    {
                        if (args[0].ToLower() == freqConstants.Value.ToLower())
                        {
                            Frequency = Convert.ToInt32(freqConstants.Key*1E+6);
                            break;
                        }
                    }
                }
            }            
        }
    }
}
