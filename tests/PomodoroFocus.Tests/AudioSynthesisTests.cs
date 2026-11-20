using System;
using PomodoroFocus.Core.Audio;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Tests
{
    public sealed class AudioSynthesisTests
    {
        public void AllNineAmbientSoundsRenderValidSamplesAndRmsInRange()
        {
            const int frames = AmbientMixer.SampleRate; // 1 full second
            var buf = new float[frames * 2];

            for (int i = 0; i < SoundCatalog.AllSoundIds.Length; i++)
            {
                string id = SoundCatalog.AllSoundIds[i];
                var mixer = new AmbientMixer(0x11223344u);
                mixer.SetMasterVolumeImmediate(100, false);
                mixer.SetTrackStateImmediate(id, true, 100);
                mixer.Render(buf, frames);

                double sumSq = 0.0;
                float peak = 0.0f;
                for (int s = 0; s < buf.Length; s++)
                {
                    float v = buf[s];
                    AssertEx.IsFalse(float.IsNaN(v) || float.IsInfinity(v), id + " sample finite");
                    float av = v < 0 ? -v : v;
                    if (av > peak) peak = av;
                    sumSq += v * v;
                }

                double rms = Math.Sqrt(sumSq / buf.Length);
                AssertEx.IsTrue(peak <= 1.0f, id + " peak <= 1.0 (was " + peak + ")");
                AssertEx.InRange(rms, 0.03, 0.35, id + " RMS in [0.03, 0.35]");
            }
        }

        public void AllFourNotificationSoundsRenderValidSamplesAndRmsInRange()
        {
            string[] notifIds = new string[] { "chime", "bowl", "wood", "digital" };
            const int frames = AmbientMixer.SampleRate; // 1 second
            var buf = new float[frames * 2];

            for (int i = 0; i < notifIds.Length; i++)
            {
                string id = notifIds[i];
                var mixer = new AmbientMixer(0x55667788u);
                mixer.SetMasterVolumeImmediate(0, true); // ambient silent
                mixer.PlayNotification(id, 100);
                mixer.Render(buf, frames);

                double sumSq = 0.0;
                float peak = 0.0f;
                for (int s = 0; s < buf.Length; s++)
                {
                    float v = buf[s];
                    AssertEx.IsFalse(float.IsNaN(v) || float.IsInfinity(v), "notif " + id + " sample finite");
                    float av = v < 0 ? -v : v;
                    if (av > peak) peak = av;
                    sumSq += v * v;
                }

                double rms = Math.Sqrt(sumSq / buf.Length);
                AssertEx.IsTrue(peak <= 1.0f, "notif " + id + " peak <= 1.0");
                AssertEx.InRange(rms, 0.03, 0.35, "notif " + id + " RMS in [0.03, 0.35]");
            }
        }

        public void MutedMixerProducesExactZeros()
        {
            const int frames = 4410;
            var buf = new float[frames * 2];
            var mixer = new AmbientMixer(0x99AABBCCu);
            mixer.SetMasterVolumeImmediate(0, true);
            mixer.SetTrackStateImmediate("white", true, 100);
            mixer.Render(buf, frames);

            for (int i = 0; i < buf.Length; i++)
            {
                AssertEx.Equal(0.0f, buf[i], "Muted sample must be 0");
            }
        }
    }
}
