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

    public static Func<T, uint, uint> GetHashCodeFunc<T>(IEqualityComparer<T>? comparer) where T : notnull
    {
        if (comparer != null)
            return (a, b) => Combine(comparer.GetHashCode(a), b);

        return static (a, b) => Combine(EqualityComparer<T>.Default.GetHashCode(a), b);
    }

    public static Func<T, uint, uint[]> GetHashCodeFunc2<T>(IEqualityComparer<T>? comparer) where T : notnull
    {
        if (comparer != null)
        {
            return (a, b) =>
            {
                unchecked
                {
                    uint val1 = Combine((uint)comparer.GetHashCode(a), b);
                    uint val2 = Murmur_32(val1);
                    uint val3 = Murmur_32(val2);

                    return [val1, val2, val3];
                }
            };
        }

        return static (a, b) =>
        {
            unchecked
            {
                uint val1 = Combine(EqualityComparer<T>.Default.GetHashCode(a), b);
                uint val2 = Murmur_32(val1);
                uint val3 = Murmur_32(val2);

                return [val1, val2, val3];
            }
        };
    }

    public static HashCode<T> GetHashCodeFunc3<T>(IEqualityComparer<T>? comparer) where T : notnull
    {
        if (comparer != null)
        {
            return (a, b, hashes) =>
            {
                unchecked
                {
                    hashes[0] = Combine((uint)comparer.GetHashCode(a), b);
                    hashes[1] = Murmur_32(hashes[0]);
                    hashes[2] = Murmur_32(hashes[1]);
                }
            };
        }

        return static (a, b, hashes) =>
        {
            unchecked
            {
                hashes[0] = Combine(EqualityComparer<T>.Default.GetHashCode(a), b);
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