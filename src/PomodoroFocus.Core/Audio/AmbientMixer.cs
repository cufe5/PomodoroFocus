using System;
using System.Collections.Generic;
using PomodoroFocus.Core.Models;

namespace PomodoroFocus.Core.Audio
{
    public sealed class AmbientMixer
    {
        public const int SampleRate = 44100;

        private sealed class TrackChannel
        {
            public IAmbientGenerator Generator;
            public bool Enabled;
            public float TargetVolume; // 0..1
            public float CurrentGain;  // 0..1 smoothed
        }

        private readonly object _sync = new object();
        private readonly TrackChannel[] _channels;
        private readonly Dictionary<string, TrackChannel> _channelMap;

        // Smoothing coefficients
        private readonly float _trackFadeCoeff;    // ~600ms toggle fade (tau ~ 200ms)
        private readonly float _gainSmoothCoeff;   // 50ms gain smoothing
        private readonly float _duckDownCoeff;     // fast ducking attack (~25ms)
        private readonly float _duckUpCoeff;       // 400ms ducking recovery (tau ~ 135ms)

        private float _masterTarget = 0.75f;
        private float _masterCurrent = 0.75f;
        private bool _masterMuted;
        private bool _playbackGateOpen = true;

        private float _duckCurrent = 1.0f;

        // Active one-shot notification buffers
        private sealed class ActiveNotification
        {
            public float[] Samples;
            public int Index;
            public float Gain;
        }
        private readonly List<ActiveNotification> _notifications = new List<ActiveNotification>();

        // Sleep timer (in remaining samples)
        private long _sleepSamplesRemaining = -1;
        private long _sleepFadeTotalSamples = SampleRate * 3L;
        private float _sleepFadeGain = 1.0f;

        public event EventHandler SleepTimerExpired;

        public AmbientMixer(uint seedBase)
        {
            _channels = new TrackChannel[SoundCatalog.AllSoundIds.Length];
            _channelMap = new Dictionary<string, TrackChannel>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < SoundCatalog.AllSoundIds.Length; i++)
            {
                string id = SoundCatalog.AllSoundIds[i];
                var ch = new TrackChannel
                {
                    Generator = AmbientGeneratorFactory.Create(id, seedBase),
                    Enabled = false,
                    TargetVolume = 0.60f,
                    CurrentGain = 0.0f
                };
                _channels[i] = ch;
                _channelMap[id] = ch;
            }

            _trackFadeCoeff = 1.0f - (float)Math.Exp(-1.0 / (0.18 * SampleRate));
            _gainSmoothCoeff = 1.0f - (float)Math.Exp(-1.0 / (0.050 * SampleRate));
            _duckDownCoeff = 1.0f - (float)Math.Exp(-1.0 / (0.025 * SampleRate));
            _duckUpCoeff = 1.0f - (float)Math.Exp(-1.0 / (0.135 * SampleRate));
        }

        public AmbientMixer() : this(0x12345678u)
        {
        }

        public void SetTrackState(string id, bool enabled, int volumePercent)
        {
            lock (_sync)
            {
                TrackChannel ch;
                if (!_channelMap.TryGetValue(id, out ch)) return;
                ch.Enabled = enabled;
                ch.TargetVolume = Clamp01(volumePercent / 100.0f);
            }
        }

        public void SetTrackStateImmediate(string id, bool enabled, int volumePercent)
        {
            lock (_sync)
            {
                TrackChannel ch;
                if (!_channelMap.TryGetValue(id, out ch)) return;
                ch.Enabled = enabled;
                ch.TargetVolume = Clamp01(volumePercent / 100.0f);
                ch.CurrentGain = enabled ? ch.TargetVolume : 0.0f;
            }
        }

        public void StopAllTracks()
        {
            lock (_sync)
            {
                for (int i = 0; i < _channels.Length; i++)
                {
                    _channels[i].Enabled = false;
                }
            }
        }

        public void SetMasterVolume(int volumePercent, bool muted)
        {
            lock (_sync)
            {
                _masterTarget = Clamp01(volumePercent / 100.0f);
                _masterMuted = muted;
            }
        }

        public void SetMasterVolumeImmediate(int volumePercent, bool muted)
        {
            lock (_sync)
            {
                _masterTarget = Clamp01(volumePercent / 100.0f);
                _masterMuted = muted;
                _masterCurrent = muted ? 0.0f : _masterTarget;
            }
        }

        public void SetPlaybackGate(bool open)
        {
            lock (_sync)
            {
                _playbackGateOpen = open;
            }
        }

        public void SetSleepTimerMinutes(int minutes)
        {
            lock (_sync)
            {
                if (minutes <= 0)
                {
                    _sleepSamplesRemaining = -1;
                    _sleepFadeGain = 1.0f;
                }
                else
                {
                    _sleepSamplesRemaining = (long)minutes * 60L * SampleRate;
                    _sleepFadeGain = 1.0f;
                }
            }
        }

        public int GetSleepTimerRemainingSeconds()
        {
            lock (_sync)
            {
                if (_sleepSamplesRemaining <= 0) return 0;
                return (int)((_sleepSamplesRemaining + SampleRate - 1) / SampleRate);
            }
        }

        public void PlayNotification(string soundId, int volumePercent)
        {
            float[] samples = NotificationSynthesizer.Synthesize(soundId);
            if (samples.Length == 0) return;
            float gain = Clamp01(volumePercent / 100.0f);
            if (gain <= 0.0001f) return;

            lock (_sync)
            {
                _notifications.Add(new ActiveNotification
                {
                    Samples = samples,
                    Index = 0,
                    Gain = gain
                });
            }
        }

        public bool HasActiveAudio()
        {
            lock (_sync)
            {
                if (_notifications.Count > 0) return true;
                if (_masterMuted || !_playbackGateOpen)
                {
                    // Check if still fading out
                    for (int i = 0; i < _channels.Length; i++)
                    {
                        if (_channels[i].CurrentGain > 0.0005f) return true;
                    }
                    return false;
                }
                for (int i = 0; i < _channels.Length; i++)
                {
                    if (_channels[i].Enabled || _channels[i].CurrentGain > 0.0005f) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Renders interleaved stereo float samples (length = frames * 2) in [-1, 1].
        /// </summary>
        public void Render(float[] interleavedOut, int frames)
        {
            bool notifySleepExpired = false;

            lock (_sync)
            {
                float masterGoal = (_masterMuted || !_playbackGateOpen) ? 0.0f : _masterTarget;

                for (int f = 0; f < frames; f++)
                {
                    // Master smoothing
                    _masterCurrent += (masterGoal - _masterCurrent) * _gainSmoothCoeff;

                    // Sleep timer countdown & 3s fade-out
                    if (_sleepSamplesRemaining > 0)
                    {
                        _sleepSamplesRemaining--;
                        if (_sleepSamplesRemaining < _sleepFadeTotalSamples)
                        {
                            _sleepFadeGain = (float)_sleepSamplesRemaining / _sleepFadeTotalSamples;
                        }
                        else
                        {
                            _sleepFadeGain = 1.0f;
                        }
                        if (_sleepSamplesRemaining == 0)
                        {
                            _sleepSamplesRemaining = -1;
                            _sleepFadeGain = 1.0f;
                            for (int i = 0; i < _channels.Length; i++)
                            {
                                _channels[i].Enabled = false;
                                _channels[i].CurrentGain = 0.0f;
                            }
                            notifySleepExpired = true;
                        }
                    }

                    // Mix notification sounds
                    float notifSample = 0.0f;
                    bool notifPlaying = false;
                    for (int n = _notifications.Count - 1; n >= 0; n--)
                    {
                        ActiveNotification an = _notifications[n];
                        notifPlaying = true;
                        notifSample += an.Samples[an.Index] * an.Gain;
                        an.Index++;
                        if (an.Index >= an.Samples.Length)
                        {
                            _notifications.RemoveAt(n);
                        }
                    }

                    // Duck ambient to 40% during notification, recover over 400ms
                    float duckGoal = notifPlaying ? 0.40f : 1.0f;
                    float duckCoeff = notifPlaying ? _duckDownCoeff : _duckUpCoeff;
                    _duckCurrent += (duckGoal - _duckCurrent) * duckCoeff;

                    // Mix ambient channels
                    float ambL = 0.0f;
                    float ambR = 0.0f;
                    float ambScale = _masterCurrent * _duckCurrent * _sleepFadeGain;

                    for (int c = 0; c < _channels.Length; c++)
                    {
                        TrackChannel ch = _channels[c];
                        float target = ch.Enabled ? ch.TargetVolume : 0.0f;
                        float coeff = ch.Enabled ? _gainSmoothCoeff : _trackFadeCoeff;
                        ch.CurrentGain += (target - ch.CurrentGain) * coeff;
                        if (ch.CurrentGain < 0.00001f && !ch.Enabled)
                        {
                            ch.CurrentGain = 0.0f;
                            continue;
                        }
                        if (ambScale > 0.00001f)
                        {
                            float sL, sR;
                            ch.Generator.NextSample(out sL, out sR);
                            ambL += sL * ch.CurrentGain;
                            ambR += sR * ch.CurrentGain;
                        }
                    }

                    float outL = SoftClip(ambL * ambScale + notifSample);
                    float outR = SoftClip(ambR * ambScale + notifSample);

                    interleavedOut[f * 2] = outL;
                    interleavedOut[f * 2 + 1] = outR;
                }
            }

            if (notifySleepExpired)
            {
                var handler = SleepTimerExpired;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Smooth soft-clipping beyond |x| > 0.8 guaranteeing output strictly in [-1, 1].
        /// </summary>
        public static float SoftClip(float x)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) return 0.0f;
            float ax = x < 0.0f ? -x : x;
            if (ax <= 0.8f) return x;
            // Smooth rational saturation in [0.8, +inf) -> [0.8, 1.0)
            float excess = ax - 0.8f;
            float compressed = 0.8f + (0.2f * excess) / (0.2f + excess * 0.6f + excess);
            if (compressed > 0.9999f) compressed = 0.9999f;
            return x < 0.0f ? -compressed : compressed;
        }

        private static float Clamp01(float v)
        {
            if (v < 0.0f) return 0.0f;
            if (v > 1.0f) return 1.0f;
            return v;
        }
    }
}
