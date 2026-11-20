using System;

namespace PomodoroFocus.Core.Audio
{
    public interface IAmbientGenerator
    {
        string Id { get; }
        void NextSample(out float left, out float right);
    }

    public static class AmbientGeneratorFactory
    {
        public const int SampleRate = 44100;

        public static IAmbientGenerator Create(string id, uint seedBase)
        {
            switch (id)
            {
                case "rain": return new RainGenerator(seedBase + 101u);
                case "ocean": return new OceanGenerator(seedBase + 202u);
                case "forest": return new ForestGenerator(seedBase + 303u);
                case "cafe": return new CafeGenerator(seedBase + 404u);
                case "fire": return new FireGenerator(seedBase + 505u);
                case "wind": return new WindGenerator(seedBase + 606u);
                case "white": return new WhiteNoiseGenerator(seedBase + 707u);
                case "brown": return new BrownNoiseGenerator(seedBase + 808u);
                case "clock": return new ClockTickGenerator(seedBase + 909u);
                default: throw new ArgumentException("Unknown sound id: " + id);
            }
        }
    }

    internal sealed class WhiteNoiseGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;

        public string Id { get { return "white"; } }

        public WhiteNoiseGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0xA5A51234u);
        }

        public void NextSample(out float left, out float right)
        {
            left = _rngL.NextWhite() * 0.25f;
            right = _rngR.NextWhite() * 0.25f;
        }
    }

    internal sealed class BrownNoiseGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private BrownNoiseFilter _bL;
        private BrownNoiseFilter _bR;

        public string Id { get { return "brown"; } }

        public BrownNoiseGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x5A5A9876u);
            // Warm up filter so immediate RMS is representative
            for (int i = 0; i < 400; i++)
            {
                _bL.Process(_rngL.NextWhite());
                _bR.Process(_rngR.NextWhite());
            }
        }

        public void NextSample(out float left, out float right)
        {
            left = _bL.Process(_rngL.NextWhite()) * 0.72f;
            right = _bR.Process(_rngR.NextWhite()) * 0.72f;
        }
    }

    internal sealed class RainGenerator : IAmbientGenerator
    {
        private struct DropVoice
        {
            public bool Active;
            public float Env;
            public float Decay;
            public float Gain;
            public StateVariableFilter Filter;
        }

        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private PinkNoiseFilter _pinkL;
        private PinkNoiseFilter _pinkR;
        private OnePoleHighPass _hpL;
        private OnePoleHighPass _hpR;
        private BrownNoiseFilter _brownL;
        private BrownNoiseFilter _brownR;
        private readonly DropVoice[] _dropsL = new DropVoice[6];
        private readonly DropVoice[] _dropsR = new DropVoice[6];
        private const float PoissonProb = 30.0f / AmbientGeneratorFactory.SampleRate;

        public string Id { get { return "rain"; } }

        public RainGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x13579BDFu);
            _hpL.SetCutoff(400.0f, AmbientGeneratorFactory.SampleRate);
            _hpR.SetCutoff(400.0f, AmbientGeneratorFactory.SampleRate);
        }

        public void NextSample(out float left, out float right)
        {
            float wL = _rngL.NextWhite();
            float wR = _rngR.NextWhite();

            float baseL = _hpL.Process(_pinkL.Process(wL)) * 0.55f + _brownL.Process(wL) * 0.15f;
            float baseR = _hpR.Process(_pinkR.Process(wR)) * 0.55f + _brownR.Process(wR) * 0.15f;

            float dropL = StepDrops(_dropsL, ref _rngL, wL);
            float dropR = StepDrops(_dropsR, ref _rngR, wR);

            left = (baseL + dropL) * 0.85f;
            right = (baseR + dropR) * 0.85f;
        }

        private static float StepDrops(DropVoice[] voices, ref XorShift32 rng, float white)
        {
            if (rng.NextFloat01() < PoissonProb)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    if (!voices[i].Active)
                    {
                        float durSec = rng.NextRange(0.008f, 0.025f);
                        voices[i].Active = true;
                        voices[i].Env = 1.0f;
                        voices[i].Decay = (float)Math.Exp(-4.0 / (durSec * AmbientGeneratorFactory.SampleRate));
                        voices[i].Gain = rng.NextRange(0.10f, 0.50f);
                        voices[i].Filter = new StateVariableFilter();
                        voices[i].Filter.SetParams(rng.NextRange(2000.0f, 6000.0f), 2.0f, AmbientGeneratorFactory.SampleRate);
                        break;
                    }
                }
            }

            float sum = 0.0f;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].Active) continue;
                float s = voices[i].Filter.ProcessBandPass(white) * voices[i].Env * voices[i].Gain;
                sum += s;
                voices[i].Env *= voices[i].Decay;
                if (voices[i].Env < 0.001f)
                {
                    voices[i].Active = false;
                }
            }
            return sum;
        }
    }

    internal sealed class OceanGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private BrownNoiseFilter _brownL;
        private BrownNoiseFilter _brownR;
        private OnePoleLowPass _lpL;
        private OnePoleLowPass _lpR;
        private double _phaseL;
        private double _phaseR;
        private double _periodL;
        private double _periodR;
        private int _cutoffCounter;

        public string Id { get { return "ocean"; } }

        public OceanGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x2468ACE0u);
            _periodL = 8.5;
            _periodR = 8.5;
            _phaseL = 2.0;        // start mid-wave for immediate presence
            _phaseR = 1.4;        // 0.6s phase lag
            _lpL.SetCutoff(900.0f, AmbientGeneratorFactory.SampleRate);
            _lpR.SetCutoff(800.0f, AmbientGeneratorFactory.SampleRate);
            for (int i = 0; i < 400; i++)
            {
                _brownL.Process(_rngL.NextWhite());
                _brownR.Process(_rngR.NextWhite());
            }
        }

        public void NextSample(out float left, out float right)
        {
            const double dt = 1.0 / AmbientGeneratorFactory.SampleRate;
            _phaseL += dt;
            if (_phaseL >= _periodL)
            {
                _phaseL -= _periodL;
                _periodL = _rngL.NextRange(7.0f, 11.0f);
            }
            _phaseR += dt;
            if (_phaseR >= _periodR)
            {
                _phaseR -= _periodR;
                _periodR = _rngR.NextRange(7.0f, 11.0f);
            }

            float eL = 0.35f + 0.65f * (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * _phaseL / _periodL));
            float eR = 0.35f + 0.65f * (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * _phaseR / _periodR));

            _cutoffCounter++;
            if (_cutoffCounter >= 64)
            {
                _cutoffCounter = 0;
                _lpL.SetCutoff(400.0f + 1400.0f * eL, AmbientGeneratorFactory.SampleRate);
                _lpR.SetCutoff(400.0f + 1400.0f * eR, AmbientGeneratorFactory.SampleRate);
            }

            float bL = _brownL.Process(_rngL.NextWhite());
            float bR = _brownR.Process(_rngR.NextWhite());

            left = _lpL.Process(bL) * eL * 1.15f;
            right = _lpR.Process(bR) * eR * 1.15f;
        }
    }

    internal sealed class ForestGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private PinkNoiseFilter _pinkL;
        private PinkNoiseFilter _pinkR;
        private OnePoleLowPass _lpL;
        private OnePoleLowPass _lpR;
        private double _lfoPhase;

        // Bird chirp state
        private int _samplesUntilNextBurst;
        private int _notesRemaining;
        private int _noteSamplesElapsed;
        private int _noteTotalSamples;
        private int _gapSamples;
        private double _birdOscPhase;
        private double _vibratoPhase;
        private float _fStart;
        private float _fEnd;
        private float _panL;
        private float _panR;

        public string Id { get { return "forest"; } }

        public ForestGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x369CF258u);
            _lpL.SetCutoff(800.0f, AmbientGeneratorFactory.SampleRate);
            _lpR.SetCutoff(800.0f, AmbientGeneratorFactory.SampleRate);
            _lfoPhase = 0.25; // start near mid-high
            _samplesUntilNextBurst = (int)(0.35f * AmbientGeneratorFactory.SampleRate);
        }

        public void NextSample(out float left, out float right)
        {
            const double dt = 1.0 / AmbientGeneratorFactory.SampleRate;
            _lfoPhase += 0.07 * dt;
            if (_lfoPhase >= 1.0) _lfoPhase -= 1.0;

            float windMod = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * _lfoPhase);
            float wL = _lpL.Process(_pinkL.Process(_rngL.NextWhite())) * (0.35f + 0.35f * windMod);
            float wR = _lpR.Process(_pinkR.Process(_rngR.NextWhite())) * (0.35f + 0.35f * windMod);

            float birdL = 0.0f;
            float birdR = 0.0f;
            StepBird(ref birdL, ref birdR);

            left = wL + birdL;
            right = wR + birdR;
        }

        private void StepBird(ref float outL, ref float outR)
        {
            if (_notesRemaining <= 0)
            {
                _samplesUntilNextBurst--;
                if (_samplesUntilNextBurst <= 0)
                {
                    _notesRemaining = 2 + (int)(_rngL.NextUInt() % 4u);
                    float pan = _rngL.NextRange(-0.6f, 0.6f);
                    _panL = 0.5f * (1.0f - pan);
                    _panR = 0.5f * (1.0f + pan);
                    StartBirdNote();
                }
                return;
            }

            if (_gapSamples > 0)
            {
                _gapSamples--;
                return;
            }

            float t = (float)_noteSamplesElapsed / _noteTotalSamples;
            float freq = _fStart + (_fEnd - _fStart) * t;
            _vibratoPhase += 30.0 / AmbientGeneratorFactory.SampleRate;
            float vib = 1.0f + 0.05f * (float)Math.Sin(2.0 * Math.PI * _vibratoPhase);
            _birdOscPhase += (freq * vib) / AmbientGeneratorFactory.SampleRate;
            if (_birdOscPhase >= 1.0) _birdOscPhase -= 1.0;

            // 5ms attack, exponential release
            int attackSamples = (int)(0.005f * AmbientGeneratorFactory.SampleRate);
            float env;
            if (_noteSamplesElapsed < attackSamples)
            {
                env = (float)_noteSamplesElapsed / attackSamples;
            }
            else
            {
                float rel = (float)(_noteSamplesElapsed - attackSamples) / (_noteTotalSamples - attackSamples + 1);
                env = (float)Math.Exp(-3.5 * rel);
            }

            float sig = (float)Math.Sin(2.0 * Math.PI * _birdOscPhase) * env * 0.16f;
            outL = sig * _panL;
            outR = sig * _panR;

            _noteSamplesElapsed++;
            if (_noteSamplesElapsed >= _noteTotalSamples)
            {
                _notesRemaining--;
                if (_notesRemaining > 0)
                {
                    _gapSamples = (int)(_rngL.NextRange(0.04f, 0.12f) * AmbientGeneratorFactory.SampleRate);
                    StartBirdNote();
                }
                else
                {
                    _samplesUntilNextBurst = (int)(_rngL.NextRange(2.0f, 7.0f) * AmbientGeneratorFactory.SampleRate);
                }
            }
        }

        private void StartBirdNote()
        {
            _noteSamplesElapsed = 0;
            _noteTotalSamples = (int)(_rngL.NextRange(0.060f, 0.180f) * AmbientGeneratorFactory.SampleRate);
            _fStart = _rngL.NextRange(2500.0f, 4500.0f);
            _fEnd = _fStart * _rngL.NextRange(0.8f, 1.3f);
            _birdOscPhase = 0;
            _vibratoPhase = 0;
        }
    }

    internal sealed class CafeGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private PinkNoiseFilter _pinkL;
        private PinkNoiseFilter _pinkR;
        private BrownNoiseFilter _brownL;
        private BrownNoiseFilter _brownR;
        private OnePoleHighPass _hpL;
        private OnePoleHighPass _hpR;
        private OnePoleLowPass _lpL;
        private OnePoleLowPass _lpR;
        private double _lfo1;
        private double _lfo2;
        private double _lfo3;

        // Clink state
        private int _samplesUntilClink;
        private bool _clinkActive;
        private int _clinkSample;
        private float _clinkPanL;
        private float _clinkPanR;

        public string Id { get { return "cafe"; } }

        public CafeGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x48C159D2u);
            _hpL.SetCutoff(250.0f, AmbientGeneratorFactory.SampleRate);
            _hpR.SetCutoff(250.0f, AmbientGeneratorFactory.SampleRate);
            _lpL.SetCutoff(1200.0f, AmbientGeneratorFactory.SampleRate);
            _lpR.SetCutoff(1200.0f, AmbientGeneratorFactory.SampleRate);
            _samplesUntilClink = (int)(0.5f * AmbientGeneratorFactory.SampleRate);
        }

        public void NextSample(out float left, out float right)
        {
            const double dt = 1.0 / AmbientGeneratorFactory.SampleRate;
            _lfo1 += 0.42 * dt;
            _lfo2 += 0.79 * dt;
            _lfo3 += 1.27 * dt;
            float modL = 0.65f + 0.12f * (float)Math.Sin(2.0 * Math.PI * _lfo1)
                               + 0.11f * (float)Math.Sin(2.0 * Math.PI * _lfo2)
                               + 0.10f * (float)Math.Sin(2.0 * Math.PI * _lfo3);
            float modR = 0.65f + 0.12f * (float)Math.Sin(2.0 * Math.PI * _lfo1 + 1.1)
                               + 0.11f * (float)Math.Sin(2.0 * Math.PI * _lfo2 + 2.3)
                               + 0.10f * (float)Math.Sin(2.0 * Math.PI * _lfo3 + 0.7);

            float wL = _rngL.NextWhite();
            float wR = _rngR.NextWhite();
            float chatterL = _lpL.Process(_hpL.Process(_pinkL.Process(wL))) * modL * 1.35f;
            float chatterR = _lpR.Process(_hpR.Process(_pinkR.Process(wR))) * modR * 1.35f;

            float baseL = _brownL.Process(wL) * 0.14f;
            float baseR = _brownR.Process(wR) * 0.14f;

            float clinkL = 0.0f;
            float clinkR = 0.0f;
            StepClink(ref clinkL, ref clinkR);

            left = chatterL + baseL + clinkL;
            right = chatterR + baseR + clinkR;
        }

        private void StepClink(ref float outL, ref float outR)
        {
            if (!_clinkActive)
            {
                _samplesUntilClink--;
                if (_samplesUntilClink <= 0)
                {
                    _clinkActive = true;
                    _clinkSample = 0;
                    float pan = _rngL.NextRange(-0.7f, 0.7f);
                    _clinkPanL = 0.5f * (1.0f - pan);
                    _clinkPanR = 0.5f * (1.0f + pan);
                }
                return;
            }

            double t = (double)_clinkSample / AmbientGeneratorFactory.SampleRate;
            float env = (float)Math.Exp(-t / 0.120);
            float sig = (float)(
                Math.Sin(2.0 * Math.PI * 2300.0 * t)
                + 0.6 * Math.Sin(2.0 * Math.PI * 3700.0 * t)
                + 0.3 * Math.Sin(2.0 * Math.PI * 5900.0 * t)) * env * 0.06f;

            outL = sig * _clinkPanL;
            outR = sig * _clinkPanR;

            _clinkSample++;
            if (env < 0.002f)
            {
                _clinkActive = false;
                _samplesUntilClink = (int)(_rngL.NextRange(3.0f, 9.0f) * AmbientGeneratorFactory.SampleRate);
            }
        }
    }

    internal sealed class FireGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private BrownNoiseFilter _brownL;
        private BrownNoiseFilter _brownR;
        private OnePoleLowPass _lpL;
        private OnePoleLowPass _lpR;
        private OnePoleHighPass _crackleHpL;
        private OnePoleHighPass _crackleHpR;

        private int _crackleRemL;
        private float _crackleGainL;
        private int _crackleRemR;
        private float _crackleGainR;

        private int _popRem;
        private int _popTotal;
        private float _popPanL;
        private float _popPanR;

        private const float CrackleProb = 6.0f / AmbientGeneratorFactory.SampleRate;
        private const float PopProb = 0.5f / AmbientGeneratorFactory.SampleRate;

        public string Id { get { return "fire"; } }

        public FireGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x5AD0F123u);
            _lpL.SetCutoff(300.0f, AmbientGeneratorFactory.SampleRate);
            _lpR.SetCutoff(300.0f, AmbientGeneratorFactory.SampleRate);
            _crackleHpL.SetCutoff(1500.0f, AmbientGeneratorFactory.SampleRate);
            _crackleHpR.SetCutoff(1500.0f, AmbientGeneratorFactory.SampleRate);
            for (int i = 0; i < 400; i++)
            {
                _brownL.Process(_rngL.NextWhite());
                _brownR.Process(_rngR.NextWhite());
            }
        }

        public void NextSample(out float left, out float right)
        {
            float wL = _rngL.NextWhite();
            float wR = _rngR.NextWhite();

            float rumbleL = _lpL.Process(_brownL.Process(wL)) * 0.72f;
            float rumbleR = _lpR.Process(_brownR.Process(wR)) * 0.72f;

            if (_crackleRemL <= 0 && _rngL.NextFloat01() < CrackleProb)
            {
                _crackleRemL = (int)(_rngL.NextRange(0.001f, 0.004f) * AmbientGeneratorFactory.SampleRate);
                _crackleGainL = _rngL.NextRange(0.20f, 0.85f);
            }
            if (_crackleRemR <= 0 && _rngR.NextFloat01() < CrackleProb)
            {
                _crackleRemR = (int)(_rngR.NextRange(0.001f, 0.004f) * AmbientGeneratorFactory.SampleRate);
                _crackleGainR = _rngR.NextRange(0.20f, 0.85f);
            }

            float crL = 0.0f;
            if (_crackleRemL > 0)
            {
                crL = _crackleHpL.Process(wL) * _crackleGainL * 0.55f;
                _crackleRemL--;
            }
            float crR = 0.0f;
            if (_crackleRemR > 0)
            {
                crR = _crackleHpR.Process(wR) * _crackleGainR * 0.55f;
                _crackleRemR--;
            }

            float popL = 0.0f;
            float popR = 0.0f;
            if (_popRem <= 0 && _rngL.NextFloat01() < PopProb)
            {
                _popTotal = (int)(0.015f * AmbientGeneratorFactory.SampleRate);
                _popRem = _popTotal;
                float pan = _rngL.NextRange(-0.5f, 0.5f);
                _popPanL = 0.5f * (1.0f - pan);
                _popPanR = 0.5f * (1.0f + pan);
            }
            if (_popRem > 0)
            {
                int elapsed = _popTotal - _popRem;
                double t = (double)elapsed / AmbientGeneratorFactory.SampleRate;
                float env = (float)Math.Exp(-t / 0.005);
                float p = (float)Math.Sin(2.0 * Math.PI * 150.0 * t) * env * 0.45f;
                popL = p * _popPanL;
                popR = p * _popPanR;
                _popRem--;
            }

            left = rumbleL + crL + popL;
            right = rumbleR + crR + popR;
        }
    }

    internal sealed class WindGenerator : IAmbientGenerator
    {
        private XorShift32 _rngL;
        private XorShift32 _rngR;
        private PinkNoiseFilter _pinkL;
        private PinkNoiseFilter _pinkR;
        private StateVariableFilter _svfL;
        private StateVariableFilter _svfR;
        private double _freqLfoL;
        private double _freqLfoR;
        private double _ampLfo;
        private int _updateCounter;

        public string Id { get { return "wind"; } }

        public WindGenerator(uint seed)
        {
            _rngL = new XorShift32(seed);
            _rngR = new XorShift32(seed ^ 0x6789ABCDu);
            _freqLfoL = 0.15;
            _freqLfoR = 0.45;
            _ampLfo = 0.25;
            _svfL.SetParams(520.0f, 1.8f, AmbientGeneratorFactory.SampleRate);
            _svfR.SetParams(610.0f, 1.8f, AmbientGeneratorFactory.SampleRate);
        }

        public void NextSample(out float left, out float right)
        {
            const double dt = 1.0 / AmbientGeneratorFactory.SampleRate;
            _freqLfoL += 0.075 * dt;
            _freqLfoR += 0.11 * dt;
            _ampLfo += 0.09 * dt;

            _updateCounter++;
            if (_updateCounter >= 64)
            {
                _updateCounter = 0;
                float fcL = 300.0f + 600.0f * (0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * _freqLfoL));
                float fcR = 300.0f + 600.0f * (0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * _freqLfoR));
                _svfL.SetParams(fcL, 1.8f, AmbientGeneratorFactory.SampleRate);
                _svfR.SetParams(fcR, 1.8f, AmbientGeneratorFactory.SampleRate);
            }

            float amp = 0.55f + 0.35f * (float)Math.Sin(2.0 * Math.PI * _ampLfo);
            left = _svfL.ProcessBandPass(_pinkL.Process(_rngL.NextWhite())) * amp * 1.15f;
            right = _svfR.ProcessBandPass(_pinkR.Process(_rngR.NextWhite())) * amp * 1.15f;
        }
    }

    internal sealed class ClockTickGenerator : IAmbientGenerator
    {
        private XorShift32 _rng;
        private int _sampleInSecond;
        private bool _isTick;

        public string Id { get { return "clock"; } }

        public ClockTickGenerator(uint seed)
        {
            _rng = new XorShift32(seed);
            // Place first tick near start so 1-second RMS is stable and immediate on activation
            _sampleInSecond = 0;
            _isTick = true;
        }

        public void NextSample(out float left, out float right)
        {
            const int sr = AmbientGeneratorFactory.SampleRate;
            float sig = 0.0f;

            // Tick / Tock alternating every 1.0s: 2000Hz / 1600Hz decaying sine + 2ms crisp mechanical click
            if (_sampleInSecond < 2200)
            {
                double t = (double)_sampleInSecond / sr;
                double freq = _isTick ? 2000.0 : 1600.0;
                float sineEnv = (float)Math.Exp(-t / 0.008);
                float clickEnv = (_sampleInSecond < 90) ? (float)Math.Exp(-t / 0.0012) : 0.0f;
                sig = (float)Math.Sin(2.0 * Math.PI * freq * t) * sineEnv * 0.72f
                    + _rng.NextWhite() * clickEnv * 0.38f;
            }

            // Very subtle mechanical escapement warmth so stereo feels alive
            float subtlePan = _isTick ? -0.15f : 0.15f;
            left = sig * (1.0f - subtlePan);
            right = sig * (1.0f + subtlePan);

            _sampleInSecond++;
            if (_sampleInSecond >= sr)
            {
                _sampleInSecond = 0;
                _isTick = !_isTick;
            }
        }
    }
}
