using System;

namespace PomodoroFocus.Core.Audio
{
    public sealed class NotificationSynthesizer
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Renders a mono notification sound buffer (<= 4s) for the given sound id:
        /// chime, bowl, wood, digital, tick (1-min warning), none.
        /// </summary>
        public static float[] Synthesize(string soundId)
        {
            if (string.IsNullOrEmpty(soundId) || soundId == "none")
            {
                return new float[0];
            }

            switch (soundId)
            {
                case "chime": return RenderChime();
                case "bowl": return RenderBowl();
                case "wood": return RenderWood();
                case "digital": return RenderDigital();
                case "tick": return RenderOneMinuteTick();
                default: return RenderChime();
            }
        }

        // C6 / E6 / G6 (1047 / 1319 / 1568 Hz) arpeggio, 120ms apart, sine + 0.2*2nd harmonic, tau = 0.9s
        private static float[] RenderChime()
        {
            int length = (int)(3.2 * SampleRate);
            var buf = new float[length];
            double[] freqs = new double[] { 1046.50, 1318.51, 1567.98 };
            double[] starts = new double[] { 0.0, 0.12, 0.24 };

            for (int n = 0; n < freqs.Length; n++)
            {
                int startSample = (int)(starts[n] * SampleRate);
                double f = freqs[n];
                for (int i = startSample; i < length; i++)
                {
                    double t = (double)(i - startSample) / SampleRate;
                    double attack = t < 0.006 ? (t / 0.006) : 1.0;
                    double env = attack * Math.Exp(-t / 0.9);
                    double s = (Math.Sin(2.0 * Math.PI * f * t) + 0.2 * Math.Sin(2.0 * Math.PI * 2.0 * f * t)) * env * 0.26;
                    buf[i] += (float)s;
                }
            }
            ClampBuffer(buf);
            return buf;
        }

        // Singing bowl: 528Hz + 531.5Hz (beating) + 1452Hz*0.3, 15ms attack, tau=2.2s
        private static float[] RenderBowl()
        {
            int length = (int)(3.8 * SampleRate);
            var buf = new float[length];
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / SampleRate;
                double attack = t < 0.015 ? (t / 0.015) : 1.0;
                double env = attack * Math.Exp(-t / 2.2);
                double s = (Math.Sin(2.0 * Math.PI * 528.0 * t)
                          + Math.Sin(2.0 * Math.PI * 531.5 * t)
                          + 0.3 * Math.Sin(2.0 * Math.PI * 1452.0 * t)) * env * 0.20;
                buf[i] = (float)s;
            }
            ClampBuffer(buf);
            return buf;
        }

        // Woodblock: two strikes (0ms, 280ms), 820Hz + 1650Hz*0.4, tau=40ms + 3ms noise click
        private static float[] RenderWood()
        {
            int length = (int)(1.0 * SampleRate);
            var buf = new float[length];
            var rng = new XorShift32(0x7700D123u);
            double[] starts = new double[] { 0.0, 0.28 };

            for (int n = 0; n < starts.Length; n++)
            {
                int startSample = (int)(starts[n] * SampleRate);
                for (int i = startSample; i < length; i++)
                {
                    double t = (double)(i - startSample) / SampleRate;
                    double attack = t < 0.002 ? (t / 0.002) : 1.0;
                    double env = attack * Math.Exp(-t / 0.045);
                    double click = (t < 0.003) ? (rng.NextWhite() * (1.0 - t / 0.003) * 0.35) : 0.0;
                    double s = (Math.Sin(2.0 * Math.PI * 820.0 * t) + 0.4 * Math.Sin(2.0 * Math.PI * 1650.0 * t)) * env * 0.58 + click;
                    buf[i] += (float)s;
                }
            }
            ClampBuffer(buf);
            return buf;
        }

        // Digital: 880Hz (sine + 0.3*3rd harmonic) 120ms on / 80ms off x2, two groups 400ms apart
        private static float[] RenderDigital()
        {
            int length = (int)(1.25 * SampleRate);
            var buf = new float[length];
            double[] starts = new double[] { 0.0, 0.20, 0.60, 0.80 };
            const double dur = 0.12;

            for (int n = 0; n < starts.Length; n++)
            {
                int s0 = (int)(starts[n] * SampleRate);
                int s1 = Math.Min(length, s0 + (int)(dur * SampleRate));
                for (int i = s0; i < s1; i++)
                {
                    double t = (double)(i - s0) / SampleRate;
                    double fade = 1.0;
                    if (t < 0.005) fade = t / 0.005;
                    else if (dur - t < 0.008) fade = (dur - t) / 0.008;
                    double s = (Math.Sin(2.0 * Math.PI * 880.0 * t) + 0.3 * Math.Sin(2.0 * Math.PI * 2640.0 * t)) * fade * 0.34;
                    buf[i] += (float)s;
                }
            }
            ClampBuffer(buf);
            return buf;
        }

        // Soft 1-minute left tick
        private static float[] RenderOneMinuteTick()
        {
            int length = (int)(0.45 * SampleRate);
            var buf = new float[length];
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / SampleRate;
                double attack = t < 0.004 ? (t / 0.004) : 1.0;
                double env = attack * Math.Exp(-t / 0.12);
                buf[i] = (float)(Math.Sin(2.0 * Math.PI * 1318.51 * t) * env * 0.22);
            }
            ClampBuffer(buf);
            return buf;
        }

        private static void ClampBuffer(float[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] > 0.98f) buf[i] = 0.98f;
                else if (buf[i] < -0.98f) buf[i] = -0.98f;
            }
        }
    }
}
