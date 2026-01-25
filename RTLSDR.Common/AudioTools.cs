using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.Common
{
    public class AudioTools
    {
        public static string GetBitRateAsString(double bitRate)
        {
            if (bitRate > 1000000)
            {
                return $"{(bitRate / 1000000).ToString("N2").PadLeft(20)}  Mb/s";
            }
            else
            {
                return $"{(bitRate / 1000).ToString("N0").PadLeft(20)}  Kb/s";
            }
        }

        /// <summary>
        /// Parsing frequency from:
        /// 108 000 000
        /// 108 Mhz
        /// 103.2 Mhz
        /// 103,2 Mhz
        /// 103 200 Khz
        /// 5A
        /// 7C
        /// </summary>
        /// <param name="command"></param>
        public static int ParseFreq(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return -1;

            command = command.Trim().ToLower();

            // ---- DAB BLOCK PARSING (e.g. "8c") ----
            if (ParseDab(command, out int dabFreq))
                return dabFreq;

            // ---- FM / NUMERIC PARSING ----
            command = command.Replace(" ", "");

            var sep = System.Globalization.CultureInfo
                            .CurrentCulture
                            .NumberFormat
                            .NumberDecimalSeparator;

            command = command.Replace(".", sep).Replace(",", sep);

            float f;

            if (command.EndsWith("khz"))
            {
                command = command.Replace("khz", "");
                if (float.TryParse(command, out f))
                    return (int)(f * 1_000);
            }

            if (command.EndsWith("mhz"))
            {
                command = command.Replace("mhz", "");
                if (float.TryParse(command, out f))
                    return (int)(f * 1_000_000);
            }

            if (int.TryParse(command, out int freq))
                return freq;

            return -1;
        }

        public static int DABMinFreq
        {
            get
            {
                return AudioTools.DabFrequenciesHz.MinBy(kvp => kvp.Value).Value;
            }
        }

        public static int DABMaxFreq
        {
            get
            {
                return AudioTools.DabFrequenciesHz.MaxBy(kvp => kvp.Value).Value;
            }
        }

        public static int FMMinFreq
        {
            get
            {
                return 88000000; // 88 MHz
            }
        }

        public static int FMMaxFreq
        {
            get
            {
                return 108000000; // 108 MHz
            }
        }

        public static int DABSampleRate
        {
            get
            {
                return 2048000; // 2 MB/s
            }
        }

        public static int FMSampleRate
        {
            get
            {
                return (int)1E06; // 1 MB/s
            }
        }

        public static readonly Dictionary<string, int> DabFrequenciesHz =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "5A", 174_928_000 }, { "5B", 176_640_000 }, { "5C", 178_352_000 }, { "5D", 180_064_000 },
            { "6A", 181_936_000 }, { "6B", 183_648_000 }, { "6C", 185_360_000 }, { "6D", 187_072_000 },
            { "7A", 188_928_000 }, { "7B", 190_640_000 }, { "7C", 192_352_000 }, { "7D", 194_064_000 },
            { "8A", 195_936_000 }, { "8B", 197_648_000 }, { "8C", 199_360_000 }, { "8D", 201_072_000 },
            { "9A", 202_928_000 }, { "9B", 204_640_000 }, { "9C", 206_352_000 }, { "9D", 208_064_000 },
            { "10A",209_936_000 }, { "10B",211_648_000 }, { "10C",213_360_000 }, { "10D",215_072_000 },
            { "11A",216_928_000 }, { "11B",218_640_000 }, { "11C",220_352_000 }, { "11D",222_064_000 },
            { "12A",223_936_000 }, { "12B",225_648_000 }, { "12C",227_360_000 }, { "12D",229_072_000 },
            { "13A",230_784_000 }, { "13B",232_496_000 }, { "13C",234_208_000 },
            { "13D",235_776_000 }, { "13E",237_488_000 }, { "13F",239_200_000 },
        };

        public static bool ParseDab(string input, out int frequencyHz)
        {
            return DabFrequenciesHz.TryGetValue(input.Trim(), out frequencyHz);
        }

        /// <summary>
        /// AI generated code for detecting station in given data (min 4 kB)
        /// Data: 96 khz PCM audio, 16 bit, stereo
        /// </summary>
        /// <param name="interleavedPcm16"></param>
        /// <returns></returns>
        public static double IsStationPresent(byte[] interleavedPcm16)
        {
            if (interleavedPcm16 == null || interleavedPcm16.Length < 4000)
                return 0;

            int sampleCount = interleavedPcm16.Length / 4; // stereo 16-bit = 4 bytes/frame
            float prev = 0f;
            int zeroCrossings = 0;

            double sumRms = 0, sumRms2 = 0;
            double totalPower = 0;
            int window = 960; // ~10 ms @ 96 kHz
            int rmsSamples = 0;

            double[] rmsBuffer = new double[sampleCount / window + 1];
            int rmsIndex = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                short left = BitConverter.ToInt16(interleavedPcm16, i * 4);
                short right = BitConverter.ToInt16(interleavedPcm16, i * 4 + 2);
                float mono = (left + right) * 0.5f / short.MaxValue;

                // Zero crossing count
                if ((mono > 0 && prev <= 0) || (mono < 0 && prev >= 0))
                    zeroCrossings++;
                prev = mono;

                // Power accumulation
                double sq = mono * mono;
                sumRms += sq;
                rmsSamples++;
                totalPower += sq;

                if (rmsSamples >= window)
                {
                    double rms = Math.Sqrt(sumRms / rmsSamples);
                    rmsBuffer[rmsIndex++] = rms;
                    sumRms = 0;
                    rmsSamples = 0;
                }
            }

            // Compute variance of RMS values (dynamics)
            int n = rmsIndex;
            if (n < 2) return 0;

            double mean = 0, var = 0;
            for (int i = 0; i < n; i++) mean += rmsBuffer[i];
            mean /= n;
            for (int i = 0; i < n; i++) var += (rmsBuffer[i] - mean) * (rmsBuffer[i] - mean);
            var /= n;

            // Average power of the signal
            double avgPower = totalPower / sampleCount;

            // Normalized zero-crossing rate
            double zcr = (double)zeroCrossings / sampleCount;

            // --- Heuristic thresholds (tune as needed) ---
            bool hasDynamics = var > 1e-5;     // real audio has changing RMS
            bool notTooNoisy = zcr < 0.15;     // noise crosses zero very often
            bool strongSignal = avgPower > 0.001; // reject weak stations or static

            //System.Console.Write($"                 [dyn: {var.ToString("N2")}, noisy: {zcr.ToString("N2")}, sign: {avgPower.ToString("N2")} ]");

            //return hasDynamics && notTooNoisy && strongSignal;
            return 100.0 - zcr * 100;
        }
    }
}
