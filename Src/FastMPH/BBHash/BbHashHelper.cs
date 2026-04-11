using Genbox.FastMPH.Internals;

namespace Genbox.FastMPH.BBHash;

internal static class BbHashHelper
{
    public static uint GetLevelHash<TKey>(TKey key, uint level, uint seed0, uint seed1, HashCode<TKey> hashCode)
    {
        uint h0 = hashCode(key, seed0);

        if (level == 0)
            return h0;

        uint h1 = hashCode(key, seed1);

        if (level == 1)
            return h1;

        for (uint i = 2; i <= level; i++)
            h1 = XorShiftNext(ref h0, ref h1);

        return h1;
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