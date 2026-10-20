using System;

namespace GridCut.Core
{
    /// <summary>
    /// Deterministic PRNG (xoshiro256** seeded via SplitMix64).
    /// One instance per generation run so results depend only on the seed.
    /// </summary>
    public sealed class Prng
    {
        private ulong _s0, _s1, _s2, _s3;

        public Prng(ulong seed)
        {
            ulong sm = seed;
            _s0 = SplitMix64(ref sm);
            _s1 = SplitMix64(ref sm);
            _s2 = SplitMix64(ref sm);
            _s3 = SplitMix64(ref sm);
        }

        private static ulong SplitMix64(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        public ulong NextULong()
        {
            ulong result = Rotl(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);
            return result;
        }

        public uint NextUInt32() => (uint)(NextULong() >> 32);

        public bool NextBool() => (NextULong() & 1UL) == 1UL;

        /// <summary>Uniform int in [0, maxExclusive). Rejection sampling, no modulo bias.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            ulong range = (ulong)maxExclusive;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
            while (true)
            {
                ulong r = NextULong();
                if (r < limit) return (int)(r % range);
            }
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive) =>
            minInclusive + NextInt(maxExclusive - minInclusive);
    }
}
