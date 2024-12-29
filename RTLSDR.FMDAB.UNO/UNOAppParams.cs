using RTLSDR.DAB;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RTLSDR.FMDAB.Console
{
    public static class UNOAppParams
    {
        public static bool Help { get; set; } = false;
        public static int ServiceNumber { get; set; } = -1;
        public static int Frequency { get; set; } = -1;

        public static bool ParseResult { get; private set; } = false;

        public static int DefaultFrequency
        {
            get
            {
                return Convert.ToInt32(DABConstants.DABFrequenciesMHz.Keys.First() * 1E+6);
            }
        }

        public static void ShowHelp()
        {
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine($"RTLSDR.FMDAB.UNO [option] ... [param] [param value] ... [param2] [param2 value]");
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine(" options: ");
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -help");
            System.Console.WriteLine();
            System.Console.WriteLine(" params: ");
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -f     \t set frequency");
            System.Console.WriteLine(" \t -freq");
            System.Console.WriteLine(" \t -frequency");
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine(" \t -sn     \t set service number (DAB only)");
            System.Console.WriteLine(" \t -snumber");
            System.Console.WriteLine(" \t -servicenumber");
        }

        public static int TryParseDABContantFreequency(string f)
        {
            foreach (var freqConstants in DABConstants.DABFrequenciesMHz)
            {
                if (f.ToLower() == freqConstants.Value.ToLower())
                {
                    return Convert.ToInt32(freqConstants.Key * 1E+6);
                }
            }

            return -1;
        }

        public static void ParseArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                ParseResult = false;
                return;
            }

            var valueExpecting = false;
            string valueExpectingParamName = null;

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
                        ParseResult = false;
                        return;
                    }

                    switch (p.Substring(1))
                    {
                        case "help":
                            Help = true;
                            break;
                        case "sn":
                        case "snumber":
                        case "servicenumber":
                            valueExpecting = true;
                            valueExpectingParamName = "sn";
                            break;
                        case "f":
                        case "freq":
                        case "frequency":
                            valueExpecting = true;
                            valueExpectingParamName = "f";
                            break;
                        default:
                            ParseResult = false;
                            return;
                    }
                }
                else
                {
                    if (valueExpecting)
                    {
                        switch (valueExpectingParamName)
                        {
                            case "f":
                                int f;
                                if (!int.TryParse(arg, out f))
                                {
                                    f = TryParseDABContantFreequency(arg);
                                    if (f == -1)
                                    {
                                        ParseResult = false;
                                        return;
                                    }
                                }
                                Frequency = f;
                                break;
                            case "sn":
                                int sn;
                                if (!int.TryParse(arg, out sn))
                                {
                                    ParseResult = false;
                                    return;
                                }
                                ServiceNumber = sn;
                                break;
                            default:
                                ParseResult = false;
                                return;
                        }

                        valueExpecting = false;
                    }
                    else
                    {
                        ParseResult = false;
                        return;
                    }
                }
            }

            if (Help)
            {
                ShowHelp();
            }

            ParseResult = true;
        }
    }
}


