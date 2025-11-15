using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.Audio
{
    public class AudioTools
    {
        /// <summary>
        /// Parsing frequency from:
        /// 108 000 000
        /// 108 Mhz
        /// 103.2 Mhz
        /// 103,2 Mhz
        /// 103 200 Khz
        /// </summary>
        /// <param name="command"></param>
        public static int ParseFreq(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return -1;

            command = command.Trim().Replace(" ", "").ToLower();

            var sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            float f;

            command = command
                            .Replace(".", sep)
                            .Replace(",", sep);

            if (command.EndsWith("khz"))
            {
                command = command.Replace("khz", "");

                if (float.TryParse(command, out f))
                {
                    command = Convert.ToInt32(f * 1000).ToString();
                }
            }
            if (command.EndsWith("mhz"))
            {
                command = command.Replace("mhz", "");

                if (float.TryParse(command, out f))
                {
                    command = Convert.ToInt32(f * 1000000).ToString();
                }
            }

            int freq;
            if (int.TryParse(command, out freq))
            {
                return freq;
            }

            return -1;
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
