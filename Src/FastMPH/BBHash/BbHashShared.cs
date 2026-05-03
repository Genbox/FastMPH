using Genbox.FastMPH.Internals;

namespace Genbox.FastMPH.BBHash;

internal static class BbHashShared
{
    private const int RankSampleBits = 512;
    internal const int RankSampleWords = RankSampleBits / 32;

    public static uint GetLevelHash<TKey>(TKey key, uint level, ulong seed, HashFunc<TKey> hashCode)
    {
        ulong h = hashCode(key, seed);
        uint s0 = (uint)h;
        uint s1 = (uint)(h >> 32);

        // Ensure the XorShift state is never all-zero (absorbing state)
        if ((s0 | s1) == 0)
        {
            s0 = 1;
            s1 = 1;
        }

        if (level == 0)
            return s0;

        if (level == 1)
            return s1;

        uint result = 0;
        for (uint i = 2; i <= level; i++)
            result = XorShiftNext(ref s0, ref s1);

        return result;
    }

    private static uint XorShiftNext(ref uint s0, ref uint s1)
    {
        unchecked
        {
            uint x = s0;
            uint y = s1;

            s0 = y;
            x ^= x << 23;
            uint next = x ^ y ^ (x >> 17) ^ (y >> 26);
            s1 = next;
            return next + y;
        }
    }
}