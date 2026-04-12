using System.Runtime.CompilerServices;

namespace Genbox.FastMPH.Internals.Helpers;

internal static class HashHelper
{
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    private static uint Combine(uint value1, uint value2)
    {
        unchecked
        {
            uint hash = 42 + Prime5;

            uint value = hash + (value1 * Prime3);
            hash = ((value << 17) | (value >> (32 - 17))) * Prime4;

            uint value3 = hash + (value2 * Prime3);
            hash = ((value3 << 17) | (value3 >> (32 - 17))) * Prime4;

            hash ^= hash >> 15;
            hash *= Prime2;
            hash ^= hash >> 13;
            hash *= Prime3;
            hash ^= hash >> 16;
            return hash;
        }
    }

    public static HashCode<T> GetHashFunc<T>(Func<T, uint> hashFunc) where T : notnull
    {
        return (a, b) => Combine(hashFunc(a), b);
    }

    public static HashCode3<T> GetHashFunc3<T>(Func<T, uint> hashFunc) where T : notnull => (a, b, hashes) =>
    {
        hashes[0] = Combine(hashFunc(a), b);
        hashes[1] = Murmur_32(hashes[0]);
        hashes[2] = Murmur_32(hashes[1]);
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Reduce(uint hash, uint range) => (uint)(((ulong)hash * range) >> 32);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Reduce(ulong hash, uint range) => (uint)Math.BigMul(hash, range, out _);

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