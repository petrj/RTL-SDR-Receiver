using System;
using System.IO;
using System.Reflection;

namespace RTLSDR.RTLSDRFMDABRadioConsoleCommon
{
    public class ConsoleAppParams
    {
        public ConsoleAppParams(string appName)
        {
            _appName = appName;
        }

        private string _appName;

        public bool Help { get; set; } = false;
        public bool FM { get; set; } = false;
        public bool DAB { get; set; } = false;
        public bool FMEmphasize { get; set; }
        public bool StdOut { get; set; } = false;

        public string InputFileName { get; set; }

        public string OutputFileName
        {
            get
            {
                var res = InputFileName;
                if (FM)
                {
                    res += ".fm.pcm";
                }
                if (DAB)
                {
                    res += ".dab.pcm";
                }
                return res;
            }
        }

        private string AppName
        {
            get
            {
                return string.IsNullOrEmpty(_appName)
                    ? AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name
                    : _appName;
            }
        }

        public void ShowError(string text)
        {
            Console.WriteLine($"Error. {text}. See help:");
            Console.WriteLine();
            Console.WriteLine($"{AppName} -help");
            Console.WriteLine();
        }

        public void ShowHelp()
        {
            Console.WriteLine($"{AppName} [option] [input file]");
            Console.WriteLine();
            //Console.WriteLine("FM/DAB demodulator");
            //Console.WriteLine();
            Console.WriteLine(" input: unsigned 8 bit integers (uint8 or u8) from rtl_sdr");
            Console.WriteLine();
            Console.WriteLine(" options: ");
            Console.WriteLine(" -fm  \t FM demodulation");
            Console.WriteLine(" -dab \t DAB demodulation");
            Console.WriteLine();
            Console.WriteLine(" -e   \t emphasize (FM only)");
            Console.WriteLine(" -stdout   \t output to STD OUT instead of file");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("example:");
            Console.WriteLine();
            Console.WriteLine($"{AppName} -fm FMdata.iq");
            Console.WriteLine(" -> output to file (raw mono 16bit) FMdata.iq.fm.pcm");
            Console.WriteLine();
            Console.WriteLine($"{AppName} -dab 7C.raw");
            Console.WriteLine(" -> output to file (raw stereo PCM 48 KHz 16bit) 7C.raw.dab.pcm");
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
                else if (p == "-fm")
                {
                    FM = true;
                }
                else if (p == "-dab")
                {
                    DAB = true;
                }
                else if (p == "-e")
                {
                    FMEmphasize = true;
                }
                else  if (p == "-stdout")
                {
                    StdOut = true;
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
