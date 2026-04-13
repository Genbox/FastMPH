using System.Runtime.CompilerServices;

namespace Genbox.FastMPH.Internals.Helpers;

internal static class HashHelper
{
    public static HashCode<T> GetHashFunc<T>(Func<T, ulong> hashFunc) where T : notnull => (a, seed) => unchecked(hashFunc(a) * seed);

    public static HashCode3<T> GetHashFunc3<T>(Func<T, ulong> hashFunc) where T : notnull => (a, seed, hashes) =>
    {
        ulong h = unchecked(hashFunc(a) * seed);
        hashes[0] = (uint)h;
        hashes[1] = (uint)(h >> 32);
        hashes[2] = Murmur_32(hashes[0] ^ hashes[1]);
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Reduce(uint hash, uint range) => (uint)(((ulong)hash * range) >> 32);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Reduce64(ulong hash, uint range) => (uint)Math.BigMul(hash, range, out _);

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