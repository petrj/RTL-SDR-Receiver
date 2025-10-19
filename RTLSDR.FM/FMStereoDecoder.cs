using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.FM
{
    using System;


    /// <summary>
    /// A.I. generated code for FM stereo decoding.
    /// </summary>
    public class FMStereoDecoder
    {
        // Konfigurace
        public int SampleRate { get; }
        public float PilotFreq = 19000f;
        public float SubcarrierFreq => PilotFreq * 2f; // 38 kHz

        // filtry
        private Biquad lowpassMono;
        private Biquad bandpassPilot;
        private Biquad bandpassStereoSub;
        private Biquad lowpassLR;

        // PLL state
        private double pllPhase = 0.0;
        private double pllFreq; // nominal phase increment (rad/sample)
        private double pllIntegrator = 0.0;
        private double pllKp = 0.02; // proportional gain (tuneable)
        private double pllKi = 0.0005; // integral gain (tuneable)

        public FMStereoDecoder(int sampleRate)
        {
            SampleRate = sampleRate;
            // inicializace filtrů (přibližné parametry)
            lowpassMono = Biquad.Lowpass(sampleRate, 15000, 0.707f); // 0-15 kHz mono
            bandpassPilot = Biquad.Bandpass(sampleRate, 19000, 2000, 0.707f); // around 19 kHz
            bandpassStereoSub = Biquad.Bandpass(sampleRate, 38000, 15000, 0.707f); // 23-53 kHz approx
            lowpassLR = Biquad.Lowpass(sampleRate, 15000, 0.707f); // LP after demod L-R

            pllFreq = 2.0 * Math.PI * PilotFreq / SampleRate;
        }

        /// <summary>
        /// Vstup: basebandSamples - float v rozsahu přibližně -1..+1 (výstup z FMDemodulate normalizovaný).
        /// Výstup: leftShort[] a rightShort[] - 16-bit PCM (short).
        /// </summary>
        public void DecodeStereoFloat(float[] basebandSamples, out short[] leftShort, out short[] rightShort)
        {
            int N = basebandSamples.Length;
            float[] mono = new float[N];
            float[] pilot = new float[N];
            float[] sub = new float[N];
            float[] lmr = new float[N];

            // 1) filtrovat L+R (mono)
            for (int i = 0; i < N; i++)
            {
                mono[i] = lowpassMono.Process(basebandSamples[i]);
            }

            // 2) extrahuj pilot (19 kHz) a stereo sub (38k band)
            for (int i = 0; i < N; i++)
            {
                pilot[i] = bandpassPilot.Process(basebandSamples[i]);
                sub[i] = bandpassStereoSub.Process(basebandSamples[i]);
            }

            // 3) PLL pro pilot - generuj stabilní 38kHz nosnou (sin(2*phi_pilot))
            // PLL: jednoduchý real-input phase detector (x * sin(phi)), lowpass integrator
            // NCO: phi += nominal + (kp * error + integrator)
            for (int i = 0; i < N; i++)
            {
                double x = pilot[i];

                // phase detector (multiply input by sin(pllPhase))
                double pd = x * Math.Sin(pllPhase);

                // loop filter
                pllIntegrator += pllKi * pd;
                double phaseAdj = pllKp * pd + pllIntegrator;

                // advance NCO (nominal + correction)
                pllPhase += pllFreq + phaseAdj;

                // wrap
                if (pllPhase > Math.PI * 2) pllPhase -= Math.PI * 2;
                if (pllPhase < 0) pllPhase += Math.PI * 2;

                // 38 kHz carrier is sin(2 * pilotPhase)
                double carrier38 = Math.Sin(2.0 * pllPhase);

                // demodulate DSB-SC: multiply subband by carrier38 -> recovers L-R (baseband)
                lmr[i] = (float)(sub[i] * carrier38);
            }

            // 4) lowpass L-R to 15 kHz
            for (int i = 0; i < N; i++)
            {
                lmr[i] = lowpassLR.Process(lmr[i]);
            }

            // 5) reconstruct left/right: L = (mono + lmr)/2 ; R = (mono - lmr)/2
            leftShort = new short[N];
            rightShort = new short[N];

            for (int i = 0; i < N; i++)
            {
                float left = 0.5f * (mono[i] + lmr[i]);
                float right = 0.5f * (mono[i] - lmr[i]);

                // převod na 16-bit PCM s oříznutím
                leftShort[i] = FloatToPcm16(left);
                rightShort[i] = FloatToPcm16(right);
            }
        }

        // helper: pokud máš vstup jako short[] (např. tvé FMDemodulate vrací short[]),
        // převede na float -1..1 a zavolá DecodeStereoFloat.
        public void DecodeStereoFromShort(short[] basebandShort, out short[] leftShort, out short[] rightShort)
        {
            float[] floatIn = new float[basebandShort.Length];
            for (int i = 0; i < basebandShort.Length; i++)
                floatIn[i] = basebandShort[i] / 32768f;
            DecodeStereoFloat(floatIn, out leftShort, out rightShort);
        }

        private static short FloatToPcm16(float v)
        {
            // oříznutí
            if (v > 1f) v = 1f;
            if (v < -1f) v = -1f;
            return (short)(v * 32767f);
        }

        // --- jednoduchý biquad implementace ---
        private class Biquad
        {
            private double a0, a1, a2, b1, b2;
            private double x1, x2, y1, y2;

            private Biquad() { }

            public double Process(double x)
            {
                double y = a0 * x + a1 * x1 + a2 * x2 - b1 * y1 - b2 * y2;
                x2 = x1; x1 = x;
                y2 = y1; y1 = y;
                return y;
            }

            public float Process(float x) => (float)Process((double)x);

            // design helpers (Butterworth style)
            public static Biquad Lowpass(int sr, double freq, double q)
            {
                var b = new Biquad();
                double w0 = 2.0 * Math.PI * freq / sr;
                double alpha = Math.Sin(w0) / (2.0 * q);
                double cosw0 = Math.Cos(w0);

                double A0 = 1 + alpha;
                b.a0 = (1 - cosw0) / 2.0 / A0;
                b.a1 = (1 - cosw0) / A0;
                b.a2 = b.a0;
                b.b1 = -2 * cosw0 / A0;
                b.b2 = (1 - alpha) / A0;
                return b;
            }

            public static Biquad Bandpass(int sr, double freq, double bw, double q)
            {
                // implementace pouziva center freq a Q (q param zde pouze pro API consistency)
                var b = new Biquad();
                double w0 = 2.0 * Math.PI * freq / sr;
                double alpha = Math.Sin(w0) / (2.0 * q);
                double cosw0 = Math.Cos(w0);
                double A0 = 1 + alpha;

                // constant skirt gain bandpass (difference form)
                b.a0 = alpha / A0;
                b.a1 = 0.0;
                b.a2 = -alpha / A0;
                b.b1 = -2.0 * cosw0 / A0;
                b.b2 = (1 - alpha) / A0;
                return b;
            }
        }
    }

}
