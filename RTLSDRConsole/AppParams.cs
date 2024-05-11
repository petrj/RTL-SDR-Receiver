using System;
using System.IO;

namespace RTLSDRConsole
{
    public struct AppParams
    {
        public bool Help { get; set; }
        public bool FM { get; set; }
        public bool DAB { get; set; }
        public bool Emphasize { get; set; }

        public string InputFileName { get; set; }

        public string OutputFileName
        {
            get
            {
                var res = InputFileName;
                if (FM)
                {
                    res += ".fm";
                }
                if (DAB)
                {
                    res += ".pcm";
                }
                return res;
            }
        }

        public static void ShowError(string text)
        {
            Console.WriteLine($"Error. {text}. See help:");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -help");
            Console.WriteLine();
        }

        public void ShowHelp()
        {
            Console.WriteLine("RTLSDRConsole.exe [option] [input file]");
            Console.WriteLine();
            Console.WriteLine("FM/DAB demodulator");
            Console.WriteLine();
            Console.WriteLine(" input file: unsigned 8 bit integers (uint8 or u8) from rtl_sdr");
            Console.WriteLine();
            Console.WriteLine(" options: ");
            Console.WriteLine(" -fm  \t FM demodulation");
            Console.WriteLine(" -dab \t DAB demodulation");
            Console.WriteLine(" -e   \t emphasize");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("example:");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -fm file.iq:");
            Console.WriteLine(" -> output is raw mono 16bit file.iq.output");
            Console.WriteLine();
            Console.WriteLine("RTLSDRConsole.exe -dab file.iq:");
            Console.WriteLine(" -> output ???");
        }

        public bool ParseArgs(string[] args)
        {
            if (args.Length == 0)
            {
                ShowError("No param specified");
                return true;
            }

            foreach (var arg in args)
            {
                var p = arg.ToLower();
                if (p.StartsWith("--", StringComparison.InvariantCulture))
                {
                    p = p.Substring(1);
                }

                if (p == "-help")
                {
                    Help = true;
                }
                else
                if (p == "-fm")
                {
                    FM = true;
                }
                else
                if (p == "-dab")
                {
                    DAB = true;
                }
                else
                if (p == "-e")
                {
                    Emphasize = true;
                }
                else
                {
                    InputFileName = arg;
                }
            }

            if (!Help && string.IsNullOrEmpty(InputFileName))
            {
                ShowError($"Input file not specified");
                return true;
            }

            if (!Help && !File.Exists(InputFileName))
            {
                ShowError($"Input file {InputFileName} does not exist");
                return true;
            }

            if (Help)
            {
                ShowHelp();
                return true;
            }

            if (!FM && !DAB)
            {
                ShowError("Missing param");
                return true;
            }

            return false;
        }
    }
}
