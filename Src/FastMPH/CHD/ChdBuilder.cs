using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.CHD.Internal;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
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
    public bool TryCreate(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out ChdState<TKey>? state, ChdSettings? settings = null)
    {
        settings ??= new ChdSettings();

        if (!TryCreateState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateState(int numKeys, ChdSettings settings, [NotNullWhen(true)]out IBuildState? state)
    {
        if (!BuildState.TryCreate(numKeys, settings, out BuildState? typed))
        {
            state = null;
            return false;
        }

        state = typed;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, ChdSettings settings, [NotNullWhen(true)]out ChdState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        LogCreating(build.NumKeys, build.NumBuckets, settings.LoadFactor, settings.KeysPerBin, settings.KeysPerBucket);

        LogMappingStep(build.NumKeys, build.NumBins);

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(hashFunc);

        if (!TryCreateCoreInternal(keys, hashCode, hashFunc, seed, settings, build, out queryState))
        {
            return false;
        }

        LogSuccess(seed);
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out ChdMinimalState<TKey>? state, ChdMinimalSettings? settings = null)
    {
        settings ??= new ChdMinimalSettings();

        if (!TryCreateMinimalState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateMinimalCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateMinimalState(int numKeys, ChdMinimalSettings settings, [NotNullWhen(true)]out IBuildState? state)
    {
        // The CHD minimal rank step reads the occupancy table as a bitset. When KeysPerBin > 1,
        // CHD uses per-bin byte counters instead, so the representation is incompatible.
        if (settings.KeysPerBin != 1)
        {
            state = null;
            return false;
        }

        if (!BuildState.TryCreate(numKeys, settings, out BuildState? typed))
        {
            state = null;
            return false;
        }

        state = typed;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, ChdMinimalSettings settings, [NotNullWhen(true)]out ChdMinimalState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        // Keep this guard in Core as well because callers can invoke it directly with a reused build state.
        if (settings.KeysPerBin != 1)
        {
            queryState = null;
            return false;
        }

        if (!TryCreateCoreInternal(keys, HashHelper.GetHashFunc3(hashFunc), hashFunc, seed, settings, build, out ChdState<TKey>? phState))
        {
            queryState = null;
            return false;
        }

        uint numBins = build.NumBins;
        uint numKeysU = build.NumKeys;
        uint numValues = numBins - numKeysU;

        Span<uint> occupTable = MemoryMarshal.Cast<byte, uint>(build.OccupTable.AsSpan());

        for (uint i = 0, idx = 0; i < numBins; i++)
        {
            if (!GetBit(occupTable, i))
                build.ValsTable[idx++] = i;
        }

        uint[] vals = GC.AllocateUninitializedArray<uint>((int)numValues);
        Array.Copy(build.ValsTable, vals, (int)numValues);
        queryState = new ChdMinimalState<TKey>(phState, new CompressedRank(vals, numValues));
        return true;
    }

    private bool TryCreateCoreInternal(ReadOnlySpan<TKey> keys, HashCode3<TKey> hashCode, HashFunc<TKey> hashFunc, ulong seed, ChdSettings settings, BuildState build, [NotNullWhen(true)]out ChdState<TKey>? queryState)
    {
        Array.Clear(build.OccupTable, 0, build.OccupTable.Length);
        Array.Clear(build.DispTable, 0, build.DispTable.Length);
        Array.Clear(build.PlacedBuckets, 0, build.PlacedBuckets.Length);

        if (!Mapping(build.MapItems, build.NumKeys, build.NumBins, build.NumBuckets, keys, hashCode, build.Buckets, build.Items, seed, out uint maxBucketSize))
        {
            LogFailed();
            queryState = null;
            return false;
        }

        LogOrderingStep();

        SortedList[] sortedLists = Ordering(ref build.Buckets, ref build.Items, build.NumBuckets, build.NumKeys, maxBucketSize);

        LogSearchingStep();

        if (!Searching(settings.UseHeuristics, settings.KeysPerBin, build.OccupTable, build.NumBins, build.Buckets, build.Items, maxBucketSize, sortedLists, build.MaxProbes, build.DispTable, build.PlacedBuckets))
        {
            LogFailed();
            queryState = null;
            return false;
        }

        LogCompressingStep();

        byte[] occupTable = GC.AllocateUninitializedArray<byte>(build.OccupTable.Length);
        Array.Copy(build.OccupTable, occupTable, occupTable.Length);

        uint[] dispTable = GC.AllocateUninitializedArray<uint>(build.DispTable.Length);
        Array.Copy(build.DispTable, dispTable, dispTable.Length);

        CompressedSequence cs = new CompressedSequence(dispTable, build.NumBuckets);
        queryState = new ChdState<TKey>(cs, build.NumBuckets, build.NumBins, build.NumKeys, seed, occupTable, hashFunc);
        return true;
    }

    private static bool Mapping<T>(MapItem[] mapItems, uint numKeys, uint numBins, uint numBuckets, ReadOnlySpan<T> keys, HashCode3<T> hashCode, Bucket[] buckets, Item[] items, ulong seed, out uint maxBucketSize)
    {
        maxBucketSize = 0;

        BucketsClean(buckets, numBuckets);

        Span<uint> hashes = stackalloc uint[3];

        uint i;
        for (i = 0; i < numKeys; i++)
        {
            T key = keys[(int)i];

            hashCode(key, seed, hashes);
            uint g = hashes[0] % numBuckets;

            ref MapItem mapItem = ref mapItems[i];
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
            ref Bucket bucket = ref buckets[i - 1];
            buckets[i].ItemsList = bucket.ItemsList + bucket.Size;
            bucket.Size = 0;
        }

        buckets[i - 1].Size = 0;

        for (i = 0; i < numKeys; i++)
        {
            if (!BucketInsert(buckets, mapItems, items, i))
                return false;
        }

        return true;
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

    private bool Searching(bool useHeuristics, byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxBucketSize, SortedList[] sortedLists, uint maxProbes, uint[] dispTable, byte[] placedBuckets)
    {
        if (useHeuristics)
            return PlaceBuckets2(keysPerBin, occupTable, numBins, buckets, items, maxBucketSize, sortedLists, maxProbes, dispTable, placedBuckets);

        return PlaceBuckets1(keysPerBin, occupTable, numBins, buckets, items, maxBucketSize, sortedLists, maxProbes, dispTable, placedBuckets);
    }

    private static bool PlaceBuckets1(byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxBucketSize, SortedList[] sortedLists, uint maxProbes, uint[] dispTable, byte[] placedBuckets)
    {
        for (uint i = maxBucketSize; i > 0; i--)
        {
            uint currBucket = sortedLists[i].BucketList;

            while (currBucket < sortedLists[i].Size + sortedLists[i].BucketList)
            {
                if (!PlaceBucket(keysPerBin, occupTable, numBins, buckets, items, maxProbes, dispTable, placedBuckets, currBucket, i))
                    return false;

                currBucket++;
            }
        }
        return true;
    }

    private static bool PlaceBucket(byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxProbes, uint[] dispTable, byte[] placedBuckets, uint bucketNum, uint size)
    {
        uint probe0Num = 0;
        uint probe1Num = 0;
        uint probeNum = 0;

        while (true)
        {
            if (PlaceBucketProbe(keysPerBin, occupTable, numBins, buckets, items, probe0Num, probe1Num, bucketNum, size))
            {
                dispTable[buckets[bucketNum].BucketId] = probe0Num + (probe1Num * numBins);
                placedBuckets[buckets[bucketNum].BucketId] = 1;
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

    private bool PlaceBuckets2(byte keysPerBin, byte[] occupTable, uint numBins, Bucket[] buckets, Item[] items, uint maxBucketSize, SortedList[] sortedLists, uint maxProbes, uint[] dispTable, byte[] placedBuckets)
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
                        placedBuckets[buckets[currBucket].BucketId] = 1;
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
                ref Item item = ref items[ptr];

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
                ref Item item = ref items[ptr];

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
                    ref Item item = ref items[ptr];

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
                    ref Item item = ref items[ptr];

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
        ref MapItem tmpMapItem = ref mapItems[itemIdx];
        ref Bucket bucket = ref buckets[tmpMapItem.BucketNum];

        uint ptr = bucket.ItemsList;
        ref Item tmpItem = ref items[ptr];

        for (uint i = 0; i < bucket.Size; i++)
        {
            if (tmpItem.F == tmpMapItem.F && tmpItem.H == tmpMapItem.H)
                return false;

            ptr++;
            tmpItem = ref items[ptr];
        }

        tmpItem.F = tmpMapItem.F;
        tmpItem.H = tmpMapItem.H;
        bucket.Size++;
        return true;
    }

    private sealed class BuildState : IBuildState
    {
        private const uint MaxProbesDefault = 1 << 20;

        public uint NumKeys;
        public uint NumBuckets;
        public uint NumBins;
        public uint MaxProbes;
        public Bucket[] Buckets = [];
        public Item[] Items = [];
        public MapItem[] MapItems = [];
        public byte[] OccupTable = [];
        public uint[] DispTable = [];
        public byte[] PlacedBuckets = [];
        public uint[] ValsTable = [];

        public static bool TryCreate(int keysLength, ChdSettings settings, [NotNullWhen(true)]out BuildState? state)
        {
            uint numKeys = (uint)keysLength;
            uint numBuckets = (numKeys / settings.KeysPerBucket) + 1;
            uint numBins = (uint)(numKeys / (settings.KeysPerBin * settings.LoadFactor)) + 1;

            if (numBins % 2 == 0)
                numBins++;

            while (!MillerRabin.IsPrimeNumber(numBins))
                numBins += 2;

            uint maxProbes = (uint)(Math.Log(Math.Max(1u, numKeys)) / Math.Log(2) / 20);

            if (maxProbes == 0)
                maxProbes = MaxProbesDefault;
            else
                maxProbes *= MaxProbesDefault;

            Bucket[] buckets = GC.AllocateUninitializedArray<Bucket>((int)numBuckets);
            uint[] dispTable = GC.AllocateUninitializedArray<uint>((int)numBuckets);
            byte[] placedBuckets = GC.AllocateUninitializedArray<byte>((int)numBuckets);
            Item[] items = GC.AllocateUninitializedArray<Item>(keysLength);
            MapItem[] mapItems = GC.AllocateUninitializedArray<MapItem>(keysLength);
            byte[] occupTable = GC.AllocateUninitializedArray<byte>((int)(settings.KeysPerBin == 1 ? ((numBins + 31) / 32) * sizeof(uint) : numBins));
            uint numValues = numBins > numKeys ? numBins - numKeys : 0;
            uint[] valsTable = GC.AllocateUninitializedArray<uint>((int)numValues);

            state = new BuildState
            {
                NumKeys = numKeys,
                NumBuckets = numBuckets,
                NumBins = numBins,
                MaxProbes = maxProbes,
                Buckets = buckets,
                DispTable = dispTable,
                PlacedBuckets = placedBuckets,
                Items = items,
                MapItems = mapItems,
                OccupTable = occupTable,
                ValsTable = valsTable
            };

            return true;
        }

        public void Reset()
        {
            Array.Clear(OccupTable, 0, OccupTable.Length);
            Array.Clear(DispTable, 0, DispTable.Length);
            Array.Clear(PlacedBuckets, 0, PlacedBuckets.Length);
        }
    }
}