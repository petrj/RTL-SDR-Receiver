using System;
using System.IO;
using System.Reflection;

namespace RTLSDR.FMDAB.Console.Common
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

        public string InputFileName { get; set; } = null;
        public string OutputFileName { get; set; } = null;

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
            System.Console.WriteLine(" input file: unsigned 8 bit integers (uint8 or u8) from rtl_sdr");
            System.Console.WriteLine();
            System.Console.WriteLine(" options: ");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -fm     \t FM demodulation");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -dab    \t DAB demodulation");
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
            System.Console.WriteLine(" \t -if     \t set input from file");
            System.Console.WriteLine(" \t -ifile");
            System.Console.WriteLine(" \t -infile");
            System.Console.WriteLine(" \t -inputfile");
            System.Console.WriteLine(" \t -ifilename");
            System.Console.WriteLine(" \t -infilename");
            System.Console.WriteLine(" \t -inputfilename");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -o      \t write output to file");
            System.Console.WriteLine(" \t -of");
            System.Console.WriteLine(" \t -ofile");
            System.Console.WriteLine(" \t -outfile");
            System.Console.WriteLine(" \t -outputfile");
            System.Console.WriteLine(" \t -ofilename");
            System.Console.WriteLine(" \t -outfilename");
            System.Console.WriteLine(" \t -outputfilename");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -s  \t set service number (DAB only)");
            System.Console.WriteLine(" \t -sn");
            System.Console.WriteLine(" \t -snumber");
            System.Console.WriteLine(" \t -servicenumber");
            System.Console.WriteLine();
            System.Console.WriteLine("examples:");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab -i 7C.raw");
            System.Console.WriteLine(" -> show DAB services");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -fm FMdata.iq");
            System.Console.WriteLine(" -> demodulate file FMdata.iq and output to file (raw mono 16bit) FMdata.iq.pcm");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab 7C.raw");
            System.Console.WriteLine(" -> demodulate file 7C.raw to file (raw stereo PCM 48 KHz 16bit) 7C.raw.pcm");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab 7C.raw");
            System.Console.WriteLine(" -> demodulate file 7C.raw to file (raw stereo PCM 48 KHz 16bit) 7C.raw.pcm");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab 7C.raw -play -s 3889");
            System.Console.WriteLine(" -> demodulate file 7C.raw and play service number 3889");
            System.Console.WriteLine();
            System.Console.WriteLine($"{AppName} -dab -ifile 7C.raw -ofile demodulated.radio.iq.data.in.wave.pcm");
            System.Console.WriteLine(" -> demodulate file 7C.raw to file (raw stereo PCM 48 KHz 16bit) 7C.raw.pcm");
        }

        public bool ParseArgs(string[] args)
        {
            //args = new string[] { "-help "};

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
                        case "s":
                        case "sn":
                        case "snumber":
                        case "servicenumber":
                            valueExpecting = true;
                            valueExpectingParamName = "s";
                            break;
                        default:
                            ShowError($"Unknown param: {p}");
                            return false;
                    }
                } else
                {
                    if (valueExpecting)
                    {
                        switch (valueExpectingParamName)
                        {
                            case "ifile":
                                InputFileName = arg;
                                break;
                            case "ofile":
                                OutputFileName = arg;
                                break;
                            case "s":
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


            if (!Help && string.IsNullOrEmpty(InputFileName))
            {
                ShowError($"Input file not specified");
                return false;
            }

            if (!Help && !File.Exists(InputFileName))
            {
                ShowError($"Input file {InputFileName} does not exist");
                return false;
            }

            if (Help)
            {
                ShowHelp();
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
                OutputFileName = InputFileName + ".pcm";
            }

            if (DAB && ServiceNumber<=0 && !Info)
            {
                ShowError("Missing DAB service number param");
                return false;
            }

            return true;
        }
    }
}


