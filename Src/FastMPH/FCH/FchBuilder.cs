using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Compat;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.FCH;

/// <summary>
/// The FCH algorithm is designed by Edward A. Fox, Qi Fan Chen, and Lenwood S. Heath. It is a one-probe algorithm designed for relatively small sets.
/// Properties:
/// <list type="bullet">
///     <item>It constructs MPHFs.</item>
///     <item>It is not order preserving.</item>
///     <item>Produce very compact functions and due to one-probe, is efficient in lookups.</item>
///     <item>It requires less than 4 bits per key.</item>
/// </list>
/// </summary>
[PublicAPI]
public sealed partial class FchBuilder<TKey> : IMinimalHashBuilder<TKey, FchMinimalState<TKey>, FchMinimalSettings> where TKey : notnull
{
    private const uint Index = 0;
    private const ulong SeedStep = 0x9E3779B97F4A7C15UL;

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, [NotNullWhen(true)]out FchMinimalState<TKey>? state, FchMinimalSettings? settings = null)
    {
        settings ??= new FchMinimalSettings();

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(hashFunc);

        FchBuildState buildState = new FchBuildState();

        return TryCreateMinimalCore(keys, hashCode, settings, seed, buildState, out state);
    }

    private bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashCode<TKey> hashCode, FchMinimalSettings settings, ulong seed0, FchBuildState buildState, [NotNullWhen(true)]out FchMinimalState<TKey>? state)
    {
        uint numItems = (uint)keys.Length;

        LogCreating(numItems, settings.BitsPerKey);

        uint b = CalculateB(settings.BitsPerKey, numItems);
        double p1 = CalculateP1(numItems);
        double p2 = CalculateP2(b);

        buildState.EnsureForLookup((int)b);
        uint[] lookupTable = buildState.LookupTable;

        LogMappingStep(seed0, b, p1, p2);
        Buckets<TKey> buckets = Mapping(seed0, b, (uint)p1, (uint)p2, numItems, keys, hashCode, buildState);

        LogOrderingStep();
        uint[] sortedIndexes = Ordering(buckets);

        LogSearchingStep();
        if (Searching(buckets, sortedIndexes, lookupTable, numItems, hashCode, seed0, settings.MaxSearchingIterations, settings.MaxSeedGenerationIterations, buildState, out ulong seed1))
        {
            LogFailed();
            state = null;
            return false;
        }

        LogSuccess(seed0, seed1);
        state = new FchMinimalState<TKey>(numItems, b, p1, p2, seed0, seed1, lookupTable, hashCode);
        return true;
    }

    private static Buckets<T> Mapping<T>(ulong seed, uint b, uint p1, uint p2, uint m, ReadOnlySpan<T> keys, HashCode<T> hashCode, FchBuildState buildState)
    {
        buildState.EnsureForBucketByKey((int)m);
        uint[] bucketByKey = buildState.BucketByKey;

        Buckets<T> buckets = new Buckets<T>(b, m);

        // First pass: count keys per bucket
        for (int i = 0; i < m; i++)
        {
            ulong hashVal = hashCode(keys[i], seed);
            uint h1 = Mixh10h11h12(b, p1, p2, (uint)(HashHelper.Mix64(hashVal) % m));

            bucketByKey[i] = h1;
            buckets.IncrementSize(h1);
        }

        buckets.ComputeStarts();

        // Second pass: insert keys into bucket positions
        for (int i = 0; i < m; i++)
            buckets.Insert(bucketByKey[i], keys[i]);

        return buckets;
    }

    private static uint[] Ordering<T>(Buckets<T> buckets) => buckets.GetIndexesSortedBySize();

    private bool Searching<T>(Buckets<T> buckets, uint[] sortedIndexes, uint[] lookupTable, uint m, HashCode<T> hashCode, ulong seed0, uint maxSearchingIterations, uint maxSeedGenerationIterations, FchBuildState buildState, out ulong seed) where T : notnull
    {
        buildState.EnsureForMaps((int)m);
        uint[] randomTable = buildState.RandomTable;
        uint[] mapTable = buildState.MapTable;
        uint iterationToGenerateH2 = 0;
        uint searchingIterations = 0;
        bool restart;
        uint numBuckets = buckets.GetNumBuckets();
        uint i;

        for (i = 0; i < m; i++)
            randomTable[i] = i;

        Permute(randomTable, m, seed0);

        for (i = 0; i < m; i++)
            mapTable[randomTable[i]] = i;

        ulong seedState = seed0;

        do
        {
            seed = GetNextSeed(ref seedState);
            restart = CheckForCollisionsH2(m, seed, buckets, sortedIndexes, hashCode, buildState);
            uint filledCount = 0;

            if (!restart)
            {
                searchingIterations++;
                iterationToGenerateH2 = 0;
            }
            else
                iterationToGenerateH2++;

            for (i = 0; i < numBuckets && !restart; i++)
            {
                uint bucketSize = buckets.GetSize(sortedIndexes[i]);
                if (bucketSize == 0)
                {
                    restart = false;
                    break;
                }

                restart = true;

                uint z;
                for (z = 0; z < m - filledCount && restart; z++)
                {
                    T key = buckets.GetKey(sortedIndexes[i], Index);

                    ulong hashVal = hashCode(key, seed);
                    uint h2 = (uint)(HashHelper.Mix64(hashVal) % m);

                    lookupTable[sortedIndexes[i]] = ((m + randomTable[filledCount + z]) - h2) % m;

                    LogSearchStatus(sortedIndexes[i], lookupTable[sortedIndexes[i]]);

                    uint j = Index;
                    uint counter = 0;
                    restart = false;

                    do
                    {
                        key = buckets.GetKey(sortedIndexes[i], j);
                        ulong hashVal2 = hashCode(key, seed);
                        h2 = (uint)(HashHelper.Mix64(hashVal2) % m);
                        uint index = (h2 + lookupTable[sortedIndexes[i]]) % m;

                        LogSearchStatus2(index, h2, bucketSize);

                        if (mapTable[index] >= filledCount)
                        {
                            uint y = mapTable[index];
                            (randomTable[y], randomTable[filledCount]) = (randomTable[filledCount], randomTable[y]);
                            mapTable[randomTable[y]] = y;
                            mapTable[randomTable[filledCount]] = filledCount;
                            filledCount++;
                            counter++;
                        }
                        else
                        {
                            restart = true; // true
                            filledCount -= counter;
                            break;
                        }
                        j = (j + 1) % bucketSize;
                    } while (j % bucketSize != Index);
                }
            }
        } while (restart && searchingIterations < maxSearchingIterations && iterationToGenerateH2 < maxSeedGenerationIterations);

        return restart;
    }

    private static ulong GetNextSeed(ref ulong seedState)
    {
        ulong seed = seedState;
        seedState = unchecked(seedState + SeedStep);
        return seed;
    }

    internal static uint Mixh10h11h12(uint b, double p1, double p2, uint initialIndex)
    {
        return Mixh10h11h12(b, (uint)p1, (uint)p2, initialIndex);
    }

    internal static uint Mixh10h11h12(uint b, uint p1, uint p2, uint initialIndex)
    {
        if (initialIndex < p1)
            initialIndex %= p2; /* h11 o h10 */
        else
        {
            /* h12 o h10 */
            initialIndex %= b;
            if (initialIndex < p2)
                initialIndex += p2;
        }

        return initialIndex;
    }

    /* Check whether function h2 causes collisions among the keys of each bucket */
    private bool CheckForCollisionsH2<T>(uint m, ulong seed, Buckets<T> buckets, uint[] sortedIndexes, HashCode<T> hashCode, FchBuildState buildState)
    {
        buildState.EnsureForHashTable((int)m);
        byte[] stampTable = buildState.StampTable;
        uint numBuckets = buckets.GetNumBuckets();
        byte stamp = 1;

        for (int i = 0; i < numBuckets; i++)
        {
            uint numKeys = buckets.GetSize(sortedIndexes[i]);
            stamp++;

            if (stamp == 0)
            {
                Array.Clear(stampTable, 0, stampTable.Length);
                stamp = 1;
            }

            LogBucket(i, numKeys);

            for (uint j = 0; j < numKeys; j++)
            {
                T key = buckets.GetKey(sortedIndexes[i], j);
                ulong hashVal = hashCode(key, seed);
                uint index = (uint)(HashHelper.Mix64(hashVal) % m);

                if (stampTable[index] == stamp)
                    return true; // collision detected

                stampTable[index] = stamp;
            }
        }
        return false;
    }

    private sealed class FchBuildState
    {
        public uint[] LookupTable = [];
        public uint[] RandomTable = [];
        public uint[] MapTable = [];
        public byte[] StampTable = [];
        public uint[] BucketByKey = [];

        public void EnsureForLookup(int size)
        {
            ArrayEnsure.EnsureCapacity(ref LookupTable, size);

            Array.Clear(LookupTable, 0, size);
        }

        public void EnsureForMaps(int size)
        {
            ArrayEnsure.EnsureCapacity(ref RandomTable, size);
            ArrayEnsure.EnsureCapacity(ref MapTable, size);
        }

        public void EnsureForHashTable(int size)
        {
            ArrayEnsure.EnsureCapacity(ref StampTable, size);
        }

        public void EnsureForBucketByKey(int size)
        {
            ArrayEnsure.EnsureCapacity(ref BucketByKey, size);
        }
    }

    private static uint CalculateB(double c, uint m) => (uint)Math.Ceiling((c * m) / ((Math.Log(m) / Math.Log(2.0)) + 1));

    private static double CalculateP1(uint m) => Math.Ceiling(0.55 * m);

    private static double CalculateP2(uint b) => Math.Ceiling(0.3 * b);

    private static void Permute(uint[] vector, uint n, ulong seed)
    {
        for (int i = 0; i < n; i++)
        {
            uint j = (uint)(NextPseudoRandom(ref seed) % n);
            (vector[i], vector[j]) = (vector[j], vector[i]);
        }
    }

    private static ulong NextPseudoRandom(ref ulong state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    private sealed class Buckets<T>
    {
        private readonly uint _numBuckets;
        private readonly uint[] _bucketSizes;
        private readonly uint[] _bucketStarts;
        private readonly T[] _keys;
        private uint _maxSize;

        public Buckets(uint numBuckets, uint numKeys)
        {
            _numBuckets = numBuckets;
            _bucketSizes = new uint[numBuckets];
            _bucketStarts = new uint[numBuckets];
            _keys = new T[numKeys];
            _maxSize = 0;
        }

        /// <summary>First pass: count keys per bucket.</summary>
        public void IncrementSize(uint index)
        {
            _bucketSizes[index]++;

            if (_bucketSizes[index] > _maxSize)
                _maxSize = _bucketSizes[index];
        }

        /// <summary>After counting, compute prefix sums and prepare for insertion.</summary>
        public void ComputeStarts()
        {
            uint offset = 0;
            for (uint i = 0; i < _numBuckets; i++)
            {
                _bucketStarts[i] = offset;
                offset += _bucketSizes[i];
            }

            // Reset sizes (will be used as insertion counters)
            Array.Clear(_bucketSizes, 0, (int)_numBuckets);
        }

        /// <summary>Second pass: insert keys into their bucket positions.</summary>
        public void Insert(uint index, T key)
        {
            uint pos = _bucketStarts[index] + _bucketSizes[index];
            _keys[pos] = key;
            _bucketSizes[index]++;

            if (_bucketSizes[index] > _maxSize)
                _maxSize = _bucketSizes[index];
        }

        public uint GetSize(uint index) => _bucketSizes[index];

        public T GetKey(uint index, uint indexKey) => _keys[_bucketStarts[index] + indexKey];

        public uint GetNumBuckets() => _numBuckets;

        public uint[] GetIndexesSortedBySize()
        {
            uint[] nbucketsSize = new uint[_maxSize + 1];
            uint[] sortedIndexes = new uint[_numBuckets];

            // collect how many buckets for each size.
            int i;
            for (i = 0; i < (int)_numBuckets; i++)
                nbucketsSize[_bucketSizes[i]]++;

            // calculating offset considering a decreasing order of buckets size.
            uint value = nbucketsSize[_maxSize];
            uint sum = 0;
            nbucketsSize[_maxSize] = sum;

            for (i = (int)_maxSize - 1; i >= 0; i--)
            {
                sum += value;
                value = nbucketsSize[i];
                nbucketsSize[i] = sum;
            }

            for (i = 0; i < (int)_numBuckets; i++)
            {
                sortedIndexes[nbucketsSize[_bucketSizes[i]]] = (uint)i;
                nbucketsSize[_bucketSizes[i]]++;
            }
            return sortedIndexes;
        }
    }
}