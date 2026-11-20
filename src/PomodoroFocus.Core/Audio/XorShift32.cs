namespace PomodoroFocus.Core.Audio
{
    public struct XorShift32
    {
        private uint _state;

        public XorShift32(uint seed)
        {
            _state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Returns uniform float in [0, 1).</summary>
        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1.0f / 16777216.0f);
        }

        /// <summary>Returns uniform white noise in [-1, 1].</summary>
        public float NextWhite()
        {
            return NextFloat01() * 2.0f - 1.0f;
        }

        public float NextRange(float min, float max)
        {
            return min + (max - min) * NextFloat01();
        }
    }
}
