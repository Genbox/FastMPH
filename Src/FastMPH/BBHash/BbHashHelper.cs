using Genbox.FastMPH.Internals;

namespace Genbox.FastMPH.BBHash;

internal static class BbHashHelper
{
    public static uint GetLevelHash<TKey>(TKey key, uint level, ulong seed, HashCode<TKey> hashCode)
    {
        ulong h = hashCode(key, seed);
        ulong s0 = (uint)h | ((ulong)(uint)h << 32);
        ulong s1 = (h >> 32) | ((h >> 32) << 32);

        if (level == 0)
            return (uint)s0;

        if (level == 1)
            return (uint)s1;

        ulong result = 0;
        for (uint i = 2; i <= level; i++)
            result = XorShiftNext(ref s0, ref s1);

        return (uint)result;
    }

    private static ulong XorShiftNext(ref ulong s0, ref ulong s1)
    {
        unchecked
        {
            ulong x = s0;
            ulong y = s1;

            s0 = y;
            x ^= x << 23;
            ulong next = x ^ y ^ (x >> 17) ^ (y >> 26);
            s1 = next;
            return next + y;
        }
    }
}