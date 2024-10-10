using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RTLSDR.FMDAB.Console
{
    public class ConsoleAppParams
    {
        public ConsoleAppParams(string appName)
        {
            _appName = appName;
        }

        private string _appName;

        public bool Help { get; set; } = false;
        public bool Play { get; set; } = false;
        public bool Info { get; set; } = false;
        public bool FM { get; set; } = false;
        public bool DAB { get; set; } = false;
        public bool FMEmphasize { get; set; }
        public bool StdOut { get; set; } = false;
        public int ServiceNumber { get; set; } = -1;

        public int Frequency { get; set; } = -1;

        public int SampleRate { get; set; } = 1000000;

        public string InputFileName { get; set; } = null;
        public string OutputFileName { get; set; } = null;

        public string OutputRawFileName { get; set; } = null;



        public InputSourceEnum InputSource = InputSourceEnum.Unknown;

        private string AppName
        {
            get
            {
                return string.IsNullOrEmpty(_appName)
                    ? AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name
                    : _appName;
            }
        }

        public bool OutputToFile
        {
            get
            {
                return !String.IsNullOrEmpty(OutputFileName);
            }
        }

        public void ShowError(string text)
        {
            System.Console.WriteLine($"Error. {text}. See help:");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -help");
            System.Console.WriteLine();
        }

        public void ShowHelp()
        {
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} [option] [option] ... [param] [param value] ... [input] [output]");
            System.Console.WriteLine();
            System.Console.WriteLine("   input samples: unsigned 8 bit integers (uint8 or u8) from rtl_sdr");
            System.Console.WriteLine("   output: demodulated raw PCM data");
            System.Console.WriteLine();
            System.Console.WriteLine(" options: ");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -fm     \t FM demodulation");
            System.Console.WriteLine("                 input ~ 1 000 000 Hz");
            System.Console.WriteLine("                 output 96 Khz, 16 bit, mono");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -dab    \t DAB demodulation");
            System.Console.WriteLine("                 input ~ 2 048 000 Hz");
            System.Console.WriteLine("                 output 48 Khz, 16 bit, stereo");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -e      \t emphasize (FM only)");
            System.Console.WriteLine(" \t -emp");
            System.Console.WriteLine(" \t -emphasize");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -i      \t analyze input and show services (DAB only)");
            System.Console.WriteLine(" \t -info");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -play      \t play audio");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -stdout \t output to STD OUT");
            System.Console.WriteLine();
            System.Console.WriteLine(" params: ");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -f     \t set frequency");
            System.Console.WriteLine(" \t -freq");
            System.Console.WriteLine(" \t -frequency");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -s         \t set sample rate");
            System.Console.WriteLine(" \t -sr          (default value is 1000000)");
            System.Console.WriteLine(" \t -samplerate");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -if     \t set input from file");
            System.Console.WriteLine(" \t -ifile");
            System.Console.WriteLine(" \t -infile");
            System.Console.WriteLine(" \t -inputfile");
            System.Console.WriteLine(" \t -ifilename");
            System.Console.WriteLine(" \t -infilename");
            System.Console.WriteLine(" \t -inputfilename");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -o      \t write output to WAVE file");
            System.Console.WriteLine(" \t -of");
            System.Console.WriteLine(" \t -ofile");
            System.Console.WriteLine(" \t -outfile");
            System.Console.WriteLine(" \t -outputfile");
            System.Console.WriteLine(" \t -ofilename");
            System.Console.WriteLine(" \t -outfilename");
            System.Console.WriteLine(" \t -outputfilename");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -oraw      \t record raw data to file");
            System.Console.WriteLine(" \t -orawfile");
            System.Console.WriteLine(" \t -outrawfile");
            System.Console.WriteLine(" \t -outputrawfile");
            System.Console.WriteLine(" \t -orawfilename");
            System.Console.WriteLine(" \t -outrawfilename");
            System.Console.WriteLine(" \t -outputrawfilename");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -sn     \t set service number (DAB only)");
            System.Console.WriteLine(" \t -snumber");
            System.Console.WriteLine(" \t -servicenumber");
            System.Console.WriteLine();
            System.Console.WriteLine("examples:");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -fm FMdata.raw");
            System.Console.WriteLine(" -> demodulate file FMdata.raw (1000K sr) to file FMdata.raw.wave (96 KHz mono 16bit)");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -fm -if FM.raw -of MyFMRadioRecord.wave");
            System.Console.WriteLine(" -> demodulate file FM.raw (1000K sr) and save to MyFMRadioRecord.wave (96 KHz, 16 bit, mono)");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -fm -play -if FM.raw");
            System.Console.WriteLine(" -> demodulate file FM.raw (1000K sr)  and play it (96 KHz, 16bit, mono)");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab -i 7C.raw");
            System.Console.WriteLine(" -> show DAB services");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab 7C.raw -s 3889");
            System.Console.WriteLine(" -> demodulate service number 3889 from file 7C.raw to 7C.raw.wave (48 KHz, 16bit, stereo) ");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab 7C.raw -s 3889 -ofile MyDABRadioRecord.wave");
            System.Console.WriteLine(" -> demodulate service number 3889 from file 7C.raw to MyDABRadioRecord.wave (48 KHz, 16bit, stereo) ");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab -play -s 3889 -if 7C.raw");
            System.Console.WriteLine(" -> demodulate service number 3889 from file 7C.raw and play it (48 KHz, 16bit, stereo)");
        }

        public bool ParseArgs(string[] args)
        {
            //args = new string[] { "-help "};

            InputSource = InputSourceEnum.Unknown;

            if (args == null || args.Length == 0)
            {
                ShowError("No param specified.");
                return false;
            }

            var valueExpecting = false;
            string valueExpectingParamName = null;
            var notDescribedParamsCount = 0;

            foreach (var arg in args)
            {
                var p = arg.ToLower().Trim();
                if (p.StartsWith("--", StringComparison.InvariantCulture))
                {
                    p = p.Substring(1);
                }

                if (p.StartsWith("-"))
                {
                    if (valueExpecting)
                    {
                        ShowError($"Expecting param: {valueExpectingParamName}");
                        return false;
                    }

                    switch (p.Substring(1))
                    {
                        case "help":
                            Help = true;
                            break;
                        case "play":
                            Play = true;
                            break;
                        case "i":
                        case "info":
                            Info = true;
                            break;
                        case "fm":
                            FM = true;
                            break;
                        case "dab":
                        case "dab+":
                            DAB = true;
                            break;
                        case "e":
                        case "emp":
                        case "emphasize":
                            DAB = true;
                            break;
                        case "stdout":
                            StdOut = true;
                            break;
                        case "if":
                        case "ifile":
                        case "infile":
                        case "inputfile":
                        case "ifilename":
                        case "infilename":
                        case "inputfilename":
                            valueExpecting = true;
                            valueExpectingParamName = "ifile";
                            break;
                        case "o":
                        case "of":
                        case "ofile":
                        case "outfile":
                        case "outputfile":
                        case "ofilename":
                        case "outfilename":
                        case "outputfilename":
                            valueExpecting = true;
                            valueExpectingParamName = "ofile";
                            break;
                        case "oraw":
                        case "orawfile":
                        case "outrawfile":
                        case "outputrawfile":
                        case "orawfilename":
                        case "outrawfilename":
                        case "outputrawfilename":
                            valueExpecting = true;
                            valueExpectingParamName = "orawfile";
                            break;

                        case "sn":
                        case "snumber":
                        case "servicenumber":
                            valueExpecting = true;
                            valueExpectingParamName = "sn";
                            break;
                        case "s":
                        case "sr":
                        case "samplerate":
                            valueExpecting = true;
                            valueExpectingParamName = "sr";
                            break;
                        case "f":
                        case "freq":
                        case "frequency":
                            valueExpecting = true;
                            valueExpectingParamName = "f";
                            break;
                        default:
                            ShowError($"Unknown param: {p}");
                            return false;
                    }
                }
                else
                {
                    if (valueExpecting)
                    {
                        switch (valueExpectingParamName)
                        {
                            case "ifile":
                                InputFileName = arg;
                                InputSource = InputSourceEnum.File;
                                break;
                            case "ofile":
                                OutputFileName = arg;
                                break;
                            case "orawfile":
                                OutputRawFileName = arg;
                                break;
                            case "sr":
                                int sr;
                                if (!int.TryParse(arg, out sr))
                                {
                                    ShowError($"Param error: {valueExpectingParamName}");
                                    return false;
                                }
                                SampleRate = sr;
                                break;
                            case "f":
                                int f;
                                if (!int.TryParse(arg, out f))
                                {
                                    ShowError($"Param error: {valueExpectingParamName}");
                                    return false;
                                }
                                Frequency = f;
                                break;
                            case "sn":
                                int sn;
                                if (!int.TryParse(arg, out sn))
                                {
                                    ShowError($"Param error: {valueExpectingParamName}");
                                    return false;
                                }
                                ServiceNumber = sn;
                                break;
                            default:
                                ShowError($"Unexpected param: {valueExpectingParamName}");
                                return false;
                        }

                        valueExpecting = false;
                    }
                    else
                    {
                        notDescribedParamsCount++;

                        if (notDescribedParamsCount == 1)
                        {
                            if (String.IsNullOrEmpty(InputFileName))
                            {
                                InputFileName = arg;
                                InputSource = InputSourceEnum.File;
                            }
                            else
                            {
                                ShowError($"Input FileName already specified");
                                return false;
                            }
                        }
                        else
                        if (notDescribedParamsCount == 2)
                        {
                            if (String.IsNullOrEmpty(OutputFileName))
                            {
                                OutputFileName = arg;
                            }
                            else
                            {
                                ShowError($"Output FileName already specified");
                                return false;
                            }
                        }
                        else
                        {
                            ShowError($"Too many parameters");
                            return false;
                        }
                    }
                }
            }

            if (Help)
            {
                ShowHelp();
                return false;
            }

            if (InputSource == InputSourceEnum.Unknown)
            {
                InputSource = InputSourceEnum.RTLDevice;
            }

            if ((InputSource == InputSourceEnum.RTLDevice) && (Frequency <= 0))
            {
                ShowError("Missing param --frequency");
                return false;
            }

            if (!FM && !DAB)
            {
                ShowError("Missing param --fm or --dab");
                return false;
            }

            if (!Info &&
                !StdOut &&
                String.IsNullOrEmpty(OutputFileName) &&
                !String.IsNullOrEmpty(InputFileName))
            {
                OutputFileName = InputFileName + ".wave";
            }

            if (DAB && ServiceNumber <= 0 && !Info)
            {
                ShowError("Missing DAB service number param");
                return false;
            }

            return true;
        }
    }
}


