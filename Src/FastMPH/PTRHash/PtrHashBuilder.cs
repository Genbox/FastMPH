using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.PTRHash;

/// <summary>
/// A PTRHash-inspired minimal perfect hash implementation with bucket pilots.
/// </summary>
[PublicAPI]
public sealed partial class PtrHashBuilder<TKey> : IMinimalHashBuilder<TKey, PtrHashMinimalState<TKey>, PtrHashMinimalSettings> where TKey : notnull
{
    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out PtrHashMinimalState<TKey>? state, PtrHashMinimalSettings? settings = null)
    {
        settings ??= new PtrHashMinimalSettings();

        if (!TryCreateMinimalState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateMinimalCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateMinimalState(int numKeys, PtrHashMinimalSettings settings, [NotNullWhen(true)]out IBuildState? state)
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
    public bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, PtrHashMinimalSettings settings, [NotNullWhen(true)]out PtrHashMinimalState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        build.Reset();
        LogCreating(keys.Length, settings.Alpha, settings.Lambda);

        bool linearBuckets = settings.BucketFunction == PtrHashBucketFunction.Linear;

        Array.Clear(build.BucketStarts, 0, build.BucketStarts.Length);

        for (int i = 0; i < keys.Length; i++)
        {
            ulong h = hashFunc(keys[i], seed);
            ulong highHash = HashHelper.Mix64(h ^ 0x9E3779B97F4A7C15UL);
            ulong lowHash = HashHelper.Mix64(h + 0xD6E8FEB86659FD93UL);

            build.LowHashes[i] = lowHash;

            uint bucket;
            if (linearBuckets)
            {
                bucket = HashHelper.Reduce64(highHash, build.NumBuckets);
            }
            else
            {
                (uint part, ulong remainder) = ReduceWithRemainder(highHash, build.NumParts);
                uint bucketInPart = HashHelper.Reduce64(ApplyBucketFunction(remainder, settings.BucketFunction), build.BucketsPerPart);
                bucket = (part * build.BucketsPerPart) + bucketInPart;
            }

            build.BucketByKey[i] = (int)bucket;
            build.BucketCounts[(int)bucket]++;
        }

        int maxBucketSize = 0;
        build.BucketStarts[0] = 0;

        for (int i = 0; i < build.BucketCounts.Length; i++)
        {
            int count = build.BucketCounts[i];
            if (count > maxBucketSize)
                maxBucketSize = count;

            build.BucketStarts[i + 1] = build.BucketStarts[i] + count;
            build.BucketOffsets[i] = build.BucketStarts[i];
            build.BucketOrder[i] = i;
        }

        build.EnsureForCandidates(maxBucketSize);

        for (int i = 0; i < keys.Length; i++)
        {
            int bucket = build.BucketByKey[i];
            int offset = build.BucketOffsets[bucket]++;
            build.BucketKeyIndices[offset] = i;
        }

        Array.Sort(build.BucketOrder, (a, b) => build.BucketCounts[b].CompareTo(build.BucketCounts[a]));

        for (int i = 0; i < build.SlotOwners.Length; i++)
            build.SlotOwners[i] = -1;
        Array.Clear(build.Pilots, 0, build.Pilots.Length);

        for (int order = 0; order < build.BucketOrder.Length; order++)
        {
            int bucket = build.BucketOrder[order];
            int bucketCount = build.BucketCounts[bucket];

            if (bucketCount == 0)
                continue;

            if (!TryPlaceBucket(
                    bucket,
                    seed,
                    settings,
                    build.EvictionQueue,
                    ref build.SlotMarkId,
                    build.BucketsPerPart,
                    build.SlotsPerPart,
                    build.SlotMultiplier,
                    build.BucketStarts,
                    build.BucketCounts,
                    build.BucketKeyIndices,
                    build.LowHashes,
                    build.SlotOwners,
                    build.Pilots,
                    build.CandidateSlots,
                    build.BestSlots,
                    build.CollidedBuckets,
                    build.BestCollidedBuckets,
                    build.RecentBuckets,
                    build.SlotMarks))
            {
                LogBucketFailure(bucket, bucketCount);
                queryState = null;
                return false;
            }
        }

        uint[] remap = BuildRemap(build.SlotOwners, build.NumKeys, build.NumSlots);
        byte[] pilots = GC.AllocateUninitializedArray<byte>(build.Pilots.Length);
        Array.Copy(build.Pilots, pilots, pilots.Length);

        queryState = new PtrHashMinimalState<TKey>(build.NumKeys, build.NumSlots, build.NumParts, build.SlotsPerPart, build.BucketsPerPart, settings.BucketFunction, seed, pilots, remap, hashFunc);

        LogSuccess(seed);
        return true;
    }

    private static bool TryPlaceBucket(
        int initialBucket,
        ulong seed,
        PtrHashMinimalSettings settings,
        PriorityQueue<int, int> evictionQueue,
        ref int slotMarkId,
        uint bucketsPerPart,
        uint slotsPerPart,
        ulong slotMultiplier,
        int[] bucketStarts,
        int[] bucketCounts,
        int[] bucketKeyIndices,
        ulong[] lowHashes,
        int[] slotOwners,
        byte[] pilots,
        int[] candidateSlots,
        int[] bestSlots,
        int[] collidedBuckets,
        int[] bestCollidedBuckets,
        int[] recentBuckets,
        int[] slotMarks)
    {
        if (TryFindCollisionFreePilot(
                initialBucket,
                seed,
                settings.MaxPilot,
                bucketsPerPart,
                slotsPerPart,
                slotMultiplier,
                bucketStarts,
                bucketKeyIndices,
                lowHashes,
                bucketCounts,
                slotOwners,
                pilots,
                candidateSlots,
                collidedBuckets,
                slotMarks,
                ref slotMarkId))
            return true;

        if (!settings.EnableEviction)
            return false;

        uint maxEvictions = settings.MaxEvictionsPerChain == 0 ? slotsPerPart * 10u : settings.MaxEvictionsPerChain;
        uint evictionCount = 0;

        evictionQueue.Clear();
        PriorityQueue<int, int> queue = evictionQueue;
        queue.Enqueue(initialBucket, -bucketCounts[initialBucket]);

        for (int i = 0; i < recentBuckets.Length; i++)
            recentBuckets[i] = -1;

        int recentIndex = 0;
        recentBuckets[0] = initialBucket;

        while (queue.Count > 0)
        {
            int bucket = queue.Dequeue();

            if (TryFindCollisionFreePilot(
                    bucket,
                    seed,
                    settings.MaxPilot,
                    bucketsPerPart,
                    slotsPerPart,
                    slotMultiplier,
                    bucketStarts,
                    bucketKeyIndices,
                    lowHashes,
                    bucketCounts,
                    slotOwners,
                    pilots,
                    candidateSlots,
                    collidedBuckets,
                    slotMarks,
                    ref slotMarkId))
                continue;

            if (!TryFindBestPilot(
                    bucket,
                    seed,
                    settings,
                    bucketsPerPart,
                    slotsPerPart,
                    slotMultiplier,
                    bucketStarts,
                    bucketKeyIndices,
                    lowHashes,
                    bucketCounts,
                    slotOwners,
                    candidateSlots,
                    bestSlots,
                    collidedBuckets,
                    bestCollidedBuckets,
                    recentBuckets,
                    slotMarks,
                    ref slotMarkId,
                    out uint selectedPilot,
                    out int collisionCount,
                    out int slotCount))
                return false;

            pilots[bucket] = (byte)selectedPilot;

            for (int i = 0; i < collisionCount; i++)
            {
                int collidedBucket = bestCollidedBuckets[i];
                if (collidedBucket == bucket)
                    continue;

                ClearBucketSlots(
                    collidedBucket,
                    seed,
                    pilots[collidedBucket],
                    bucketsPerPart,
                    slotsPerPart,
                    slotMultiplier,
                    bucketStarts,
                    bucketKeyIndices,
                    lowHashes,
                    slotOwners);

                queue.Enqueue(collidedBucket, -bucketCounts[collidedBucket]);

                evictionCount++;
                if (evictionCount > maxEvictions)
                    return false;
            }

            for (int i = 0; i < slotCount; i++)
                slotOwners[bestSlots[i]] = bucket;

            recentIndex++;
            if (recentIndex >= recentBuckets.Length)
                recentIndex = 0;

            recentBuckets[recentIndex] = bucket;
        }

        return true;
    }

    private static bool TryFindCollisionFreePilot(
        int bucket,
        ulong seed,
        uint maxPilot,
        uint bucketsPerPart,
        uint slotsPerPart,
        ulong slotMultiplier,
        int[] bucketStarts,
        int[] bucketKeyIndices,
        ulong[] lowHashes,
        int[] bucketCounts,
        int[] slotOwners,
        byte[] pilots,
        int[] candidateSlots,
        int[] collidedBuckets,
        int[] slotMarks,
        ref int markId)
    {
        int slotCount = bucketCounts[bucket];

        for (uint pilot = 0; pilot < maxPilot; pilot++)
        {
            int currentMark = NextMarkId(slotMarks, ref markId);

            bool valid = TryEvaluatePilot(
                bucket,
                seed,
                (byte)pilot,
                bucketsPerPart,
                slotsPerPart,
                slotMultiplier,
                bucketStarts,
                bucketKeyIndices,
                lowHashes,
                bucketCounts,
                slotOwners,
                candidateSlots,
                collidedBuckets,
                null,
                false,
                long.MaxValue,
                out _,
                out _,
                slotMarks,
                currentMark);

            if (!valid)
                continue;

            pilots[bucket] = (byte)pilot;

            for (int i = 0; i < slotCount; i++)
                slotOwners[candidateSlots[i]] = bucket;

            return true;
        }

        return false;
    }

    private static bool TryFindBestPilot(
        int bucket,
        ulong seed,
        PtrHashMinimalSettings settings,
        uint bucketsPerPart,
        uint slotsPerPart,
        ulong slotMultiplier,
        int[] bucketStarts,
        int[] bucketKeyIndices,
        ulong[] lowHashes,
        int[] bucketCounts,
        int[] slotOwners,
        int[] candidateSlots,
        int[] bestSlots,
        int[] collidedBuckets,
        int[] bestCollidedBuckets,
        int[] recentBuckets,
        int[] slotMarks,
        ref int markId,
        out uint selectedPilot,
        out int selectedCollisionCount,
        out int selectedSlotCount)
    {
        selectedPilot = 0;
        selectedCollisionCount = 0;
        selectedSlotCount = 0;

        long bestScore = long.MaxValue;
        uint maxPilot = settings.MaxPilot;
        uint startPilot = 0;

        if (settings.RandomizePilotSearchStart)
            startPilot = (uint)(HashHelper.Mix64((uint)bucket ^ seed) % maxPilot);

        int bucketSize = bucketCounts[bucket];
        for (uint delta = 0; delta < maxPilot; delta++)
        {
            uint pilot = startPilot + delta;
            if (pilot >= maxPilot)
                pilot -= maxPilot;

            int currentMark = NextMarkId(slotMarks, ref markId);

            bool valid = TryEvaluatePilot(
                bucket,
                seed,
                (byte)pilot,
                bucketsPerPart,
                slotsPerPart,
                slotMultiplier,
                bucketStarts,
                bucketKeyIndices,
                lowHashes,
                bucketCounts,
                slotOwners,
                candidateSlots,
                collidedBuckets,
                recentBuckets,
                true,
                bestScore,
                out int collisionCount,
                out long score,
                slotMarks,
                currentMark);

            if (!valid)
                continue;

            if (score >= bestScore)
                continue;

            bestScore = score;
            selectedPilot = pilot;
            selectedCollisionCount = collisionCount;
            selectedSlotCount = bucketSize;

            Array.Copy(candidateSlots, 0, bestSlots, 0, bucketSize);
            Array.Copy(collidedBuckets, 0, bestCollidedBuckets, 0, collisionCount);

            if (score == (long)bucketSize * bucketSize)
                break;
        }

        return bestScore != long.MaxValue;
    }

    private static bool TryEvaluatePilot(
        int bucket,
        ulong seed,
        byte pilot,
        uint bucketsPerPart,
        uint slotsPerPart,
        ulong slotMultiplier,
        int[] bucketStarts,
        int[] bucketKeyIndices,
        ulong[] lowHashes,
        int[] bucketCounts,
        int[] slotOwners,
        int[] candidateSlots,
        int[] collidedBuckets,
        int[]? recentBuckets,
        bool allowCollisions,
        long scoreCutoff,
        out int collisionCount,
        out long score,
        int[] slotMarks,
        int markId)
    {
        int start = bucketStarts[bucket];
        int bucketSize = bucketCounts[bucket];
        uint part = (uint)bucket / bucketsPerPart;
        uint partOffset = part * slotsPerPart;
        ulong pilotHash = PilotMix(pilot, seed);

        collisionCount = 0;
        score = 0;

        for (int i = 0; i < bucketSize; i++)
        {
            int keyIndex = bucketKeyIndices[start + i];
            int slot = (int)(partOffset + ReduceFastMod32(lowHashes[keyIndex] ^ pilotHash, slotsPerPart, slotMultiplier));

            if (slotMarks[slot] == markId)
                return false;

            candidateSlots[i] = slot;
            slotMarks[slot] = markId;

            int owner = slotOwners[slot];
            if (owner < 0 || owner == bucket)
                continue;

            if (recentBuckets is not null && ContainsRecentBucket(recentBuckets, owner))
                return false;

            if (!TryAddUnique(collidedBuckets, ref collisionCount, owner))
                continue;

            score += (long)bucketCounts[owner] * bucketCounts[owner];
            if (score >= scoreCutoff)
                return false;
        }

        return allowCollisions || collisionCount == 0;
    }

    private static int NextMarkId(int[] slotMarks, ref int markId)
    {
        markId++;

        if (markId == 0)
        {
            Array.Clear(slotMarks, 0, slotMarks.Length);
            markId = 1;
        }

        return markId;
    }

    private static bool ContainsRecentBucket(int[] recentBuckets, int bucket)
    {
        for (int i = 0; i < recentBuckets.Length; i++)
        {
            if (recentBuckets[i] == bucket)
                return true;
        }

        return false;
    }

    private static bool TryAddUnique(int[] values, ref int count, int value)
    {
        for (int i = 0; i < count; i++)
        {
            if (values[i] == value)
                return false;
        }

        values[count] = value;
        count++;
        return true;
    }

    private static void ClearBucketSlots(
        int bucket,
        ulong seed,
        byte pilot,
        uint bucketsPerPart,
        uint slotsPerPart,
        ulong slotMultiplier,
        int[] bucketStarts,
        int[] bucketKeyIndices,
        ulong[] lowHashes,
        int[] slotOwners)
    {
        int start = bucketStarts[bucket];
        int end = bucketStarts[bucket + 1];
        uint part = (uint)bucket / bucketsPerPart;
        uint partOffset = part * slotsPerPart;
        ulong pilotHash = PilotMix(pilot, seed);

        for (int i = start; i < end; i++)
        {
            int keyIndex = bucketKeyIndices[i];
            int slot = (int)(partOffset + ReduceFastMod32(lowHashes[keyIndex] ^ pilotHash, slotsPerPart, slotMultiplier));

            if (slotOwners[slot] == bucket)
                slotOwners[slot] = -1;
        }
    }

    private static uint[] BuildRemap(int[] slotOwners, uint numKeys, uint numSlots)
    {
        if (numSlots <= numKeys)
            return [];

        uint[] remap = new uint[numSlots - numKeys];
        uint[] freeLow = new uint[numSlots - numKeys];
        uint freeCount = 0;

        for (uint i = 0; i < numKeys; i++)
        {
            if (slotOwners[i] < 0)
                freeLow[freeCount++] = i;
        }

        uint remapIndex = 0;
        for (uint i = numKeys; i < numSlots && remapIndex < freeCount; i++)
        {
            if (slotOwners[i] >= 0)
                remap[i - numKeys] = freeLow[remapIndex++];
        }

        return remap;
    }

    private sealed class BuildState : IBuildState
    {
        public ulong[] LowHashes = [];
        public int[] BucketByKey = [];
        public int[] BucketCounts = [];
        public int[] BucketStarts = [];
        public int[] BucketOffsets = [];
        public int[] BucketKeyIndices = [];
        public int[] BucketOrder = [];
        public int[] SlotOwners = [];
        public int[] RecentBuckets = [];
        public int[] CandidateSlots = [];
        public int[] BestSlots = [];
        public int[] CollidedBuckets = [];
        public int[] BestCollidedBuckets = [];
        public int[] SlotMarks = [];
        public int SlotMarkId;
        public PriorityQueue<int, int> EvictionQueue = new PriorityQueue<int, int>();
        public ulong SlotMultiplier;
        public uint NumBuckets;
        public byte[] Pilots = [];

        public uint NumKeys;
        public uint NumSlots;
        public uint NumParts;
        public uint SlotsPerPart;
        public uint BucketsPerPart;

        public static bool TryCreate(int numKeys, PtrHashMinimalSettings settings, [NotNullWhen(true)]out BuildState? state)
        {
            uint numParts = ComputeParts((uint)numKeys, settings);
            uint slotsPerPart = Math.Max(1u, (uint)Math.Ceiling(numKeys / (settings.Alpha * numParts)));
            if (IsPowerOfTwo(slotsPerPart))
                slotsPerPart++;

            uint bucketsPerPart = Math.Max(1u, (uint)Math.Ceiling(numKeys / (settings.Lambda * numParts))) + 3u;

            if (slotsPerPart > int.MaxValue / numParts || bucketsPerPart > int.MaxValue / numParts)
            {
                state = null;
                return false;
            }

            uint numSlots = checked(slotsPerPart * numParts);
            uint numBuckets = checked(bucketsPerPart * numParts);

            state = new BuildState
            {
                NumKeys = (uint)numKeys,
                NumSlots = numSlots,
                NumParts = numParts,
                SlotsPerPart = slotsPerPart,
                BucketsPerPart = bucketsPerPart,
                NumBuckets = numBuckets,
                SlotMultiplier = ComputeFastModMultiplier(slotsPerPart),
                LowHashes = GC.AllocateUninitializedArray<ulong>(numKeys),
                BucketByKey = GC.AllocateUninitializedArray<int>(numKeys),
                BucketKeyIndices = GC.AllocateUninitializedArray<int>(numKeys),
                BucketCounts = GC.AllocateUninitializedArray<int>((int)numBuckets),
                BucketStarts = GC.AllocateUninitializedArray<int>((int)numBuckets + 1),
                BucketOffsets = GC.AllocateUninitializedArray<int>((int)numBuckets),
                BucketOrder = GC.AllocateUninitializedArray<int>((int)numBuckets),
                Pilots = GC.AllocateUninitializedArray<byte>((int)numBuckets),
                SlotOwners = GC.AllocateUninitializedArray<int>((int)numSlots),
                SlotMarks = GC.AllocateUninitializedArray<int>((int)numSlots),
                RecentBuckets = GC.AllocateUninitializedArray<int>(settings.RecentEvictionWindow),
                EvictionQueue = new PriorityQueue<int, int>(),
                CandidateSlots = [],
                BestSlots = [],
                CollidedBuckets = [],
                BestCollidedBuckets = []
            };

            return true;
        }

        public void EnsureForCandidates(int maxBucketSize)
        {
            ArrayEnsure.EnsureCapacity(ref CandidateSlots, maxBucketSize);
            ArrayEnsure.EnsureCapacity(ref BestSlots, maxBucketSize);
            ArrayEnsure.EnsureCapacity(ref CollidedBuckets, maxBucketSize);
            ArrayEnsure.EnsureCapacity(ref BestCollidedBuckets, maxBucketSize);
        }

        public void Reset()
        {
            Array.Clear(BucketCounts, 0, BucketCounts.Length);
            Array.Clear(BucketOffsets, 0, BucketOffsets.Length);
            Array.Clear(SlotMarks, 0, SlotMarks.Length);
            SlotMarkId = 0;
            for (int i = 0; i < SlotOwners.Length; i++)
                SlotOwners[i] = -1;
            Array.Clear(Pilots, 0, Pilots.Length);
            EvictionQueue.Clear();
            for (int i = 0; i < RecentBuckets.Length; i++)
                RecentBuckets[i] = -1;
        }

        private static uint ComputeParts(uint numKeys, PtrHashMinimalSettings settings)
        {
            if (settings.Parts > 0)
                return settings.Parts;

            if (numKeys == 0)
                return 1;

            double value = Math.Ceiling(numKeys / (double)settings.TargetKeysPerPart);
            return Math.Max(1u, (uint)value);
        }

        private static ulong ComputeFastModMultiplier(uint range) => unchecked((ulong.MaxValue / range) + 1UL);

        private static bool IsPowerOfTwo(uint value) => value != 0 && (value & (value - 1)) == 0;
    }

    private static (uint Reduced, ulong Remainder) ReduceWithRemainder(ulong hash, uint range)
    {
        uint reduced = (uint)Math.BigMul(hash, range, out ulong remainder);
        return (reduced, remainder);
    }

    private static uint ReduceFastMod32(ulong hash, uint range, ulong multiplier)
    {
        ulong lowBits = unchecked(multiplier * hash);
        return (uint)Math.BigMul(lowBits, range, out _);
    }

    private static ulong PilotMix(byte pilot, ulong seed) => unchecked(0x517CC1B727220A95UL * (pilot ^ seed));

    private static ulong ApplyBucketFunction(ulong hash, PtrHashBucketFunction bucketFunction) => bucketFunction switch
    {
        PtrHashBucketFunction.Linear => hash,
        PtrHashBucketFunction.SquareEps => (Math.BigMul(hash, hash, out _) / 256UL * 255UL) + (hash / 256UL),
        PtrHashBucketFunction.CubicEps => (Math.BigMul(Math.BigMul(hash, hash, out _), (hash >> 1) | (1UL << 63), out _) / 256UL * 255UL) + (hash / 256UL),
        _ => hash
    };
}