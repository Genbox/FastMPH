using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.CHD.Internal;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Compat;
using JetBrains.Annotations;
using static Genbox.FastMPH.CHD.Internal.BitBool;

namespace Genbox.FastMPH.CHD;

/// <summary>
/// The CHD algorithm is designed by Djamal Belazzougui, Fabiano C. Botelho, and Martin Dietzfelbinger. It is based on Compress, Hash, Displace.
/// Properties:
/// <list type="bullet">
///     <item>It constructs both PHFs and MPHFs in linear time.</item>
///     <item>It can generate MPHFs that can be stored in approximately 2.07 bits per key.</item>
///     <item>It can generate PHFs with a load factor up to 99 %.</item>
/// </list>
/// </summary>
[PublicAPI]
public sealed partial class ChdBuilder<TKey> : IMinimalHashBuilder<TKey, ChdMinimalState<TKey>, ChdMinimalSettings>, IHashBuilder<TKey, ChdState<TKey>, ChdSettings> where TKey : notnull
{
    /// <inheritdoc />
    public bool TryCreate(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out ChdState<TKey>? state, ChdSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new ChdSettings();
        comparer ??= EqualityComparer<TKey>.Default;

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(comparer);

        uint numKeys = (uint)keys.Length;
        uint numBuckets = (numKeys / settings.KeysPerBucket) + 1;

        LogCreating(numKeys, numBuckets, settings.LoadFactor, settings.KeysPerBin, settings.KeysPerBucket);

        uint numBins = (uint)(numKeys / (settings.KeysPerBin * settings.LoadFactor)) + 1;

        //Round the number of bins to the prime immediately above
        if (numBins % 2 == 0)
            numBins++;

        //Genbox: Made the method call the condition in the while
        while (!MillerRabin.IsPrimeNumber(numBins))
            numBins += 2; // just odd numbers can be primes for n > 2

        // We allocate the working tables
        Bucket[] buckets = new Bucket[numBuckets];

        for (int i = 0; i < numBuckets; i++)
            buckets[i] = new Bucket();

        Item[] items = new Item[numKeys];

        for (int i = 0; i < numKeys; i++)
            items[i] = new Item();

        //TODO: Make configurable via settings
        uint maxProbes = (uint)(Math.Log(numKeys) / Math.Log(2) / 20);

        const uint maxProbesDefault = 1 << 20; // default value for max_probes

        if (maxProbes == 0)
            maxProbes = maxProbesDefault;
        else
            maxProbes *= maxProbesDefault;

        //Genbox: refactored this a bit
        uint size = settings.KeysPerBin == 1 ? ((numBins + 31) / 32) * sizeof(uint) : numBins * sizeof(uint);

        byte[] occupTable = new byte[size];
        uint[] dispTable = new uint[numBuckets];

        uint iterations = settings.Iterations;
        uint seed = 0;

        //Genbox: converted the while loop to a for loop for readability
        for (; iterations > 0; iterations--)
        {
            LogMappingStep(numKeys, numBins);

            if (!ChdBuilder<TKey>.Mapping(numKeys, numBins, numBuckets, keys, hashCode, buckets, items, out uint maxBucketSize, out seed))
            {
                LogFailed();
                state = null;
                return false;
            }

            LogOrderingStep();

            SortedList[] sortedLists = Ordering(ref buckets, ref items, numBuckets, numKeys, maxBucketSize);

            LogSearchingStep();

            if (Searching(settings.UseHeuristics, settings.KeysPerBin, occupTable, numBins, buckets, items, maxBucketSize, sortedLists, maxProbes, dispTable))
                break;

            //Genbox: We don't need to check on KeysPerBin here as Array.Clear() clears the whole array from its length
            Array2.Clear(occupTable);
        }

        //Genbox: I moved this condition out of the while loop above.
        if (iterations == 0)
        {
            LogFailed();
            state = null;
            return false;
        }

        LogCompressingStep();
        CompressedSequence cs = new CompressedSequence(dispTable, numBuckets);

        LogSuccess(seed);
        state = new ChdState<TKey>(cs, numBuckets, numBins, numKeys, seed, occupTable, hashCode);
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out ChdMinimalState<TKey>? state, ChdMinimalSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new ChdMinimalSettings();

        if (!TryCreate(keys, out ChdState<TKey>? phState, settings))
        {
            state = null;
            return false;
        }

        uint numBins = phState.Bins;
        uint numKeys = phState.NumKeys;
        uint numValues = numBins - numKeys;

        uint[] valsTable = new uint[numValues];
        Span<uint> occupTable = MemoryMarshal.Cast<byte, uint>(phState.OccupTable.AsSpan());

        for (uint i = 0, idx = 0; i < numBins; i++)
        {
            if (!GetBit(occupTable, i))
                valsTable[idx++] = i;
        }

        CompressedRank cr = new CompressedRank(valsTable, numValues);
        state = new ChdMinimalState<TKey>(phState, cr);
        return true;
    }

    private static bool Mapping<T>(uint numKeys, uint numBins, uint numBuckets, ReadOnlySpan<T> keys, HashCode3<T> hashCode, Bucket[] buckets, Item[] items, out uint maxBucketSize, out uint seed)
    {
        maxBucketSize = 0;

        //TODO: Move this allocation out
        MapItem[] mapItems = new MapItem[numKeys];

        for (int i = 0; i < numKeys; i++)
            mapItems[i] = new MapItem();

        uint mappingIterations = 1000;

        //Genbox: converted the while loop to a for loop for readability
        for (; mappingIterations > 0; mappingIterations--)
        {
            //Genbox: It used mod here to reduce keyspace of the seed. I don't see why that is needed.
            seed = RandomHelper.Next();

            BucketsClean(buckets, numBuckets);

            uint[] hashes = new uint[3];

            uint i;
            for (i = 0; i < numKeys; i++)
            {
                T key = keys[(int)i];

                hashCode(key, seed, hashes);
                uint g = hashes[0] % numBuckets;

                MapItem mapItem = mapItems[i];
                mapItem.F = hashes[1] % numBins;
                mapItem.H = (hashes[2] % (numBins - 1)) + 1;
                mapItem.BucketNum = g;
                buckets[g].Size++;

                if (buckets[g].Size > maxBucketSize)
                    maxBucketSize = buckets[g].Size;
            }

            buckets[0].ItemsList = 0;

            for (i = 1; i < numBuckets; i++)
            {
                Bucket bucket = buckets[i - 1];
                buckets[i].ItemsList = bucket.ItemsList + bucket.Size;
                bucket.Size = 0;
            }

            buckets[i - 1].Size = 0;

            for (i = 0; i < numKeys; i++)
            {
                if (!BucketInsert(buckets, mapItems, items, i))
                    break;
            }

            if (i == numKeys)
                return true;
        }

        seed = 0;
        return false;
    }

    private SortedList[] Ordering(ref Bucket[] inputBuckets, ref Item[] inputItems, uint numBuckets, uint numItems, uint maxBucketSize)
    {
        LogMaxBucketSize(maxBucketSize);

        SortedList[] sortedLists = new SortedList[maxBucketSize + 1];

        for (int k = 0; k < maxBucketSize + 1; k++)
            sortedLists[k] = new SortedList();

        uint i;
        uint bucketSize;

        // Determine size of each list of buckets
        for (i = 0; i < numBuckets; i++)
        {
            bucketSize = inputBuckets[i].Size;
            if (bucketSize == 0)
                continue;
            sortedLists[bucketSize].Size++;
        }

        sortedLists[1].BucketList = 0;

        // Determine final position of list of buckets into the contiguous array that will store all the buckets
        for (i = 2; i <= maxBucketSize; i++)
        {
            sortedLists[i].BucketList = sortedLists[i - 1].BucketList + sortedLists[i - 1].Size;
            sortedLists[i - 1].Size = 0;
        }

        sortedLists[i - 1].Size = 0;

        // Store the buckets in a new array which is sorted by bucket sizes
        Bucket[] outputBuckets = new Bucket[numBuckets];

        for (int k = 0; k < numBuckets; k++)
            outputBuckets[k] = new Bucket();

        uint position;
        for (i = 0; i < numBuckets; i++)
        {
            bucketSize = inputBuckets[i].Size;
            if (bucketSize == 0)
                continue;

            position = sortedLists[bucketSize].BucketList + sortedLists[bucketSize].Size;
            outputBuckets[position].BucketId = i;
            outputBuckets[position].ItemsList = inputBuckets[i].ItemsList;
            sortedLists[bucketSize].Size++;
        }

        // Return the buckets sorted in new order and free the old buckets sorted in old order
        inputBuckets = outputBuckets;

        // Store the items according to the new order of buckets.
        Item[] outputItems = new Item[numItems];

        for (int k = 0; k < numItems; k++)
            outputItems[k] = new Item();

        position = 0;
        for (bucketSize = 1; bucketSize <= maxBucketSize; bucketSize++)
        {
            for (i = sortedLists[bucketSize].BucketList; i < sortedLists[bucketSize].Size + sortedLists[bucketSize].BucketList; i++)
            {
                uint position2 = outputBuckets[i].ItemsList;
                outputBuckets[i].ItemsList = position;
                for (uint j = 0; j < bucketSize; j++)
                {
                    outputItems[position].F = inputItems[position2].F;
                    outputItems[position].H = inputItems[position2].H;
                    position++;
                    position2++;
                }
            }
        }

        //Return the items sorted in new order and free the old items sorted in old order
        inputItems = outputItems;
        return sortedLists;
    }

    private bool Searching(bool useHeuristics, byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxBucketSize, SortedList[] sortedLists, uint maxProbes, uint[] dispTable)
    {
        //TODO: use a delegate to point to the correct method to avoid branching
        if (useHeuristics)
            return PlaceBuckets2(keysPerBin, occupTable, numBins, buckets, items, maxBucketSize, sortedLists, maxProbes, dispTable);

        return PlaceBuckets1(keysPerBin, occupTable, numBins, buckets, items, maxBucketSize, sortedLists, maxProbes, dispTable);
    }

    private bool PlaceBuckets1(byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxBucketSize, SortedList[] sortedLists, uint maxProbes, uint[] dispTable)
    {
        for (uint i = maxBucketSize; i > 0; i--)
        {
            uint currBucket = sortedLists[i].BucketList;

            while (currBucket < sortedLists[i].Size + sortedLists[i].BucketList)
            {
                if (!PlaceBucket(keysPerBin, occupTable, numBins, buckets, items, maxProbes, dispTable, currBucket, i))
                    return false;

                currBucket++;
            }
        }
        return true;
    }

    private static bool PlaceBucket(byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxProbes, uint[] dispTable, uint bucketNum, uint size)
    {
        uint probe0Num = 0;
        uint probe1Num = 0;
        uint probeNum = 0;

        while (true)
        {
            if (PlaceBucketProbe(keysPerBin, occupTable, numBins, buckets, items, probe0Num, probe1Num, bucketNum, size))
            {
                dispTable[buckets[bucketNum].BucketId] = probe0Num + (probe1Num * numBins);
                return true;
            }
            probe0Num++;
            if (probe0Num >= numBins)
            {
                probe0Num -= numBins;
                probe1Num++;
            }
            probeNum++;
            if (probeNum >= maxProbes || probe1Num >= numBins)
                return false;
        }
    }

    private bool PlaceBuckets2(byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxBucketSize, SortedList[] sortedLists, uint maxProbes, uint[] dispTable)
    {
        uint i;

        LogUsingHeuristics();

        for (i = maxBucketSize; i > 0; i--)
        {
            uint probeNum = 0;
            uint probe0Num = 0;
            uint probe1Num = 0;
            uint sortedListSize = sortedLists[i].Size;

            while (sortedLists[i].Size != 0)
            {
                uint currBucket = sortedLists[i].BucketList;
                uint j;
                uint nonPlacedBucket;
                for (j = 0, nonPlacedBucket = 0; j < sortedLists[i].Size; j++)
                {
                    // if bucket is successfully placed remove it from list
                    if (PlaceBucketProbe(keysPerBin, occupTable, numBins, buckets, items, probe0Num, probe1Num, currBucket, i))
                    {
                        dispTable[buckets[currBucket].BucketId] = probe0Num + (probe1Num * numBins);
                        LogDisplacement(currBucket, dispTable[currBucket]);
                    }
                    else
                    {
                        LogNotPlaced(currBucket);
#if DEBUG
                        uint itemsList = buckets[nonPlacedBucket + sortedLists[i].BucketList].ItemsList;
                        uint bucketId = buckets[nonPlacedBucket + sortedLists[i].BucketList].BucketId;
#endif
                        buckets[nonPlacedBucket + sortedLists[i].BucketList].ItemsList = buckets[currBucket].ItemsList;
                        buckets[nonPlacedBucket + sortedLists[i].BucketList].BucketId = buckets[currBucket].BucketId;
#if DEBUG
                        buckets[currBucket].ItemsList = itemsList;
                        buckets[currBucket].BucketId = bucketId;
#endif
                        nonPlacedBucket++;
                    }
                    currBucket++;
                }

                sortedLists[i].Size = nonPlacedBucket;
                probe0Num++;
                if (probe0Num >= numBins)
                {
                    probe0Num -= numBins;
                    probe1Num++;
                }

                probeNum++;
                if (probeNum >= maxProbes || probe1Num >= numBins)
                {
                    sortedLists[i].Size = sortedListSize;
                    return false;
                }
            }

            sortedLists[i].Size = sortedListSize;
        }

        return true;
    }

    private static bool PlaceBucketProbe(byte keysPerBin, byte[] occupTable, uint n, Bucket[] buckets, Item[] items, uint probe0Num, uint probe1Num, uint bucketNum, uint size)
    {
        uint i;
        uint position;

        uint ptr = buckets[bucketNum].ItemsList;
        Span<uint> occup = MemoryMarshal.Cast<byte, uint>(occupTable.AsSpan());

        // try place bucket with probe_num
        if (keysPerBin > 1)
        {
            for (i = 0; i < size; i++) // placement
            {
                Item item = items[ptr];

                position = (uint)((item.F + ((ulong)item.H * probe0Num) + probe1Num) % n);

                if (occupTable[position] >= keysPerBin)
                    break;

                occupTable[position]++;
                ptr++;
            }
        }
        else
        {
            for (i = 0; i < size; i++) // placement
            {
                Item item = items[ptr];

                position = (uint)((item.F + ((ulong)item.H * probe0Num) + probe1Num) % n);
                if (GetBit(occup, position))
                    break;

                SetBit(occup, position);
                ptr++;
            }
        }

        if (i != size) // Undo the placement
        {
            ptr = buckets[bucketNum].ItemsList;
            if (keysPerBin > 1)
            {
                while (true)
                {
                    Item item = items[ptr];

                    if (i == 0)
                        break;
                    position = (uint)((item.F + ((ulong)item.H * probe0Num) + probe1Num) % n);
                    occupTable[position]--;
                    ptr++;
                    i--;
                }
            }
            else
            {
                while (true)
                {
                    Item item = items[ptr];

                    if (i == 0)
                        break;

                    position = (uint)((item.F + ((ulong)item.H * probe0Num) + probe1Num) % n);
                    UnsetBit(occup, position);

                    ptr++;
                    i--;
                }
            }
            return false;
        }
        return true;
    }

    private static void BucketsClean(Bucket[] buckets, uint numBuckets)
    {
        for (uint i = 0; i < numBuckets; i++)
            buckets[i].Size = 0;
    }

    private static bool BucketInsert(Bucket[] buckets, MapItem[] mapItems, Item[] items, uint itemIdx)
    {
        MapItem tmpMapItem = mapItems[itemIdx];
        Bucket bucket = buckets[tmpMapItem.BucketNum];

        uint ptr = bucket.ItemsList;
        Item tmpItem = items[ptr];

        for (uint i = 0; i < bucket.Size; i++)
        {
            if (tmpItem.F == tmpMapItem.F && tmpItem.H == tmpMapItem.H)
                return false;

            ptr++;
            tmpItem = items[ptr];
        }

        tmpItem.F = tmpMapItem.F;
        tmpItem.H = tmpMapItem.H;
        bucket.Size++;
        return true;
    }

    private sealed class Bucket
    {
        public uint ItemsList; // offset
        public uint Size;

        public uint BucketId
        {
            get => Size;
            set => Size = value;
        }
    }

    private sealed class SortedList
    {
        public uint BucketList;
        public uint Size;
    }

    private sealed class Item
    {
        public uint F;
        public uint H;
    }

    private sealed class MapItem
    {
        public uint BucketNum;
        public uint F;
        public uint H;
    }

    private static class MillerRabin
    {
        public static bool IsPrimeNumber(ulong n)
        {
            if (n % 2 == 0)
                return false;
            if (n % 3 == 0)
                return false;
            if (n % 5 == 0)
                return false;
            if (n % 7 == 0)
                return false;

            ulong s = 0;
            ulong d = n - 1;

            do
            {
                s++;
                d /= 2;
            } while (d % 2 == 0);

            ulong a = 2;
            if (!CheckWitness(IntPow(a, d, n), n, s))
                return false;
            a = 7;
            if (!CheckWitness(IntPow(a, d, n), n, s))
                return false;
            a = 61;
            return CheckWitness(IntPow(a, d, n), n, s);
        }

        private static ulong IntPow(ulong a, ulong d, ulong n)
        {
            ulong aPow = a;
            ulong res = 1;
            while (d > 0)
            {
                if ((d & 1) == 1)
                    res = (res * aPow) % n;
                aPow = (aPow * aPow) % n;
                d /= 2;
            }
            return res;
        }

        private static bool CheckWitness(ulong aExpD, ulong n, ulong s)
        {
            ulong aExp = aExpD;
            if (aExp == 1 || aExp == n - 1)
                return true;

            for (ulong i = 1; i < s; ++i)
            {
                aExp = (aExp * aExp) % n;
                if (aExp == n - 1)
                    return true;
            }
            return false;
        }
    }
}