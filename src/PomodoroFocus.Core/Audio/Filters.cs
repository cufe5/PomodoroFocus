using System;

namespace PomodoroFocus.Core.Audio
{
    public struct PinkNoiseFilter
    {
        private float _b0;
        private float _b1;
        private float _b2;

        public float Process(float white)
        {
            _b0 = 0.99765f * _b0 + white * 0.0990460f;
            _b1 = 0.96300f * _b1 + white * 0.2965164f;
            _b2 = 0.57000f * _b2 + white * 1.0526913f;
            return (_b0 + _b1 + _b2 + white * 0.1848f) * 0.18f;
        }
    }

    public struct BrownNoiseFilter
    {
        private float _b;

        public float Process(float white)
        {
            _b = (_b + 0.02f * white) / 1.02f;
            return _b * 3.5f;
        }
    }

    public struct OnePoleLowPass
    {
        private float _z;
        private float _alpha;

        public void SetCutoff(float cutoffHz, float sampleRate)
        {
            if (cutoffHz < 10.0f) cutoffHz = 10.0f;
            float maxHz = sampleRate * 0.45f;
            if (cutoffHz > maxHz) cutoffHz = maxHz;
            float dt = 1.0f / sampleRate;
            float rc = 1.0f / (2.0f * (float)Math.PI * cutoffHz);
            _alpha = dt / (rc + dt);
        }

        public float Process(float input)
        {
            _z += _alpha * (input - _z);
            return _z;
        }
    }

    public struct OnePoleHighPass
    {
        private float _prevIn;
        private float _prevOut;
        private float _alpha;

        public void SetCutoff(float cutoffHz, float sampleRate)
        {
            if (cutoffHz < 10.0f) cutoffHz = 10.0f;
            float maxHz = sampleRate * 0.45f;
            if (cutoffHz > maxHz) cutoffHz = maxHz;
            float dt = 1.0f / sampleRate;
            float rc = 1.0f / (2.0f * (float)Math.PI * cutoffHz);
            _alpha = rc / (rc + dt);
        }

        public float Process(float input)
        {
            float y = _alpha * (_prevOut + input - _prevIn);
            _prevIn = input;
            _prevOut = y;
            return y;
        }
    }

    /// <summary>Chamberlin State-Variable Filter (BandPass / LowPass / HighPass).</summary>
    public struct StateVariableFilter
    {
        private float _low;
        private float _band;
        private float _f;
        private float _qInv;

        public void SetParams(float centerHz, float q, float sampleRate)
        {
            if (centerHz < 20.0f) centerHz = 20.0f;
            float maxHz = sampleRate * 0.22f; // stable Chamberlin SVF limit
            if (centerHz > maxHz) centerHz = maxHz;
            if (q < 0.5f) q = 0.5f;
            _f = 2.0f * (float)Math.Sin(Math.PI * centerHz / sampleRate);
            _qInv = 1.0f / q;
        }

        public float ProcessBandPass(float input)
        {
            _low += _f * _band;
            float high = input - _low - _qInv * _band;
            _band += _f * high;
            // Clamp against numerical blowup
            if (_band > 10.0f) _band = 10.0f;
            else if (_band < -10.0f) _band = -10.0f;
            if (_low > 10.0f) _low = 10.0f;
            else if (_low < -10.0f) _low = -10.0f;
            return _band;
        }
    }
}
