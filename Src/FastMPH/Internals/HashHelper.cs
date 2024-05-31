using System.Runtime.CompilerServices;
using Genbox.FastMPH.BDZ;

namespace Genbox.FastMPH.Internals;

internal static class HashHelper
{
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    private static uint Combine<T1, T2>(T1 value1, T2 value2) where T1 : notnull where T2 : notnull
    {
        unchecked
        {
            uint hc1 = (uint)value1.GetHashCode();
            uint hc2 = (uint)value2.GetHashCode();

            uint hash = 42 + Prime5;

            uint value = hash + hc1 * Prime3;
            hash = ((value << 17) | (value >> (32 - 17))) * Prime4;

            uint value3 = hash + hc2 * Prime3;
            hash = ((value3 << 17) | (value3 >> (32 - 17))) * Prime4;

            hash ^= hash >> 15;
            hash *= Prime2;
            hash ^= hash >> 13;
            hash *= Prime3;
            hash ^= hash >> 16;
            return hash;
        }
    }

    public static HashCode<T> GetHashFunc<T>(IEqualityComparer<T> comparer) where T : notnull
    {
        return (a, b) => Combine(comparer.GetHashCode(a), b);
    }

    public static HashCode3<T> GetHashFunc3<T>(IEqualityComparer<T> comparer) where T : notnull
    {
        return (a, b, hashes) =>
        {
            unchecked
            {
                hashes[0] = Combine(comparer.GetHashCode(a), b);
                hashes[1] = Murmur_32(hashes[0]);
                hashes[2] = Murmur_32(hashes[1]);
            }
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Murmur_32(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }
    }
}