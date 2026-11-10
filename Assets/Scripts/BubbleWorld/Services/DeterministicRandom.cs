using System;

namespace BubbleWorld
{
    public sealed class DeterministicRandom
    {
        private Random random;

        public DeterministicRandom(int seed)
        {
            random = new Random(seed);
        }

        public DeterministicRandom(string seed)
        {
            random = new Random(seed.GetHashCode());
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        public int RangeInclusive(int minInclusive, int maxInclusive)
        {
            return random.Next(minInclusive, maxInclusive + 1);
        }

        public float Value01()
        {
            return (float)random.NextDouble();
        }

        public float Range(float min, float max)
        {
            return (float)(min + random.NextDouble() * (max - min));
        }

        public void ReSeed(int seed)
        {
            random = new Random(seed);
        }
    }
}
