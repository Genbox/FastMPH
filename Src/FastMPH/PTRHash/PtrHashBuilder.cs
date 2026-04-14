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
public sealed partial class PtrHashBuilder<TKey> : IMinimalHashBuilder<TKey, PtrHashMinimalState<TKey>, PtrHashMinimalSettings>, IPartialHashBuilder<TKey, PtrHashMinimalState<TKey>, PtrHashMinimalSettings> where TKey : notnull
{
    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, [NotNullWhen(true)]out PtrHashMinimalState<TKey>? state, PtrHashMinimalSettings? settings = null)
    {
        PartialBuildStatus status = CreatePartial(keys, hashFunc, seed, out PartialBuildResult<TKey, PtrHashMinimalState<TKey>>? result, settings);

        if (status == PartialBuildStatus.Success)
        {
            state = result!.State;
            return true;
        }

        state = null;
        return false;
    }

    /// <inheritdoc />
    public PartialBuildStatus CreatePartial(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, out PartialBuildResult<TKey, PtrHashMinimalState<TKey>>? result, PtrHashMinimalSettings? settings = null)
    {
        settings ??= new PtrHashMinimalSettings();

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(hashFunc);
        PtrHashBuildState buildState = new PtrHashBuildState();

        return CreatePartialCore(keys, hashCode, settings, seed, buildState, out result);
    }

    private PartialBuildStatus CreatePartialCore(ReadOnlySpan<TKey> keys, HashCode<TKey> hashCode, PtrHashMinimalSettings settings, ulong seed, PtrHashBuildState buildState, out PartialBuildResult<TKey, PtrHashMinimalState<TKey>>? result)
    {
        LogCreating(keys.Length, settings.Alpha, settings.Lambda);

        uint numKeys = (uint)keys.Length;
        uint numParts = ComputeParts(numKeys, settings);
        uint slotsPerPart = Math.Max(1u, (uint)Math.Ceiling(numKeys / (settings.Alpha * numParts)));
        if (IsPowerOfTwo(slotsPerPart))
            slotsPerPart++;

        uint bucketsPerPart = Math.Max(1u, (uint)Math.Ceiling(numKeys / (settings.Lambda * numParts))) + 3u;

        if (slotsPerPart > int.MaxValue / numParts || bucketsPerPart > int.MaxValue / numParts)
        {
            result = null;
            return PartialBuildStatus.Failure;
        }

        uint numSlots = checked(slotsPerPart * numParts);
        uint numBuckets = checked(bucketsPerPart * numParts);
        ulong slotMultiplier = ComputeFastModMultiplier(slotsPerPart);

        buildState.EnsureForKeys(keys.Length);
        buildState.EnsureForBuckets((int)numBuckets);
        buildState.EnsureForSlots((int)numSlots);
        buildState.EnsureForEviction(settings.RecentEvictionWindow);

        ulong[] lowHashes = buildState.LowHashes;
        int[] bucketByKey = buildState.BucketByKey;
        int[] bucketCounts = buildState.BucketCounts;
        int[] bucketStarts = buildState.BucketStarts;
        int[] bucketOffsets = buildState.BucketOffsets;
        int[] bucketKeyIndices = buildState.BucketKeyIndices;
        int[] bucketOrder = buildState.BucketOrder;
        int[] slotOwners = buildState.SlotOwners;
        int[] bucketOwnedCounts = buildState.BucketOwnedCounts;
        byte[] pilots = buildState.Pilots;
        int[] recentBuckets = buildState.RecentBuckets;

        Array.Clear(bucketCounts, 0, bucketCounts.Length);

        bool linearBuckets = settings.BucketFunction == PtrHashBucketFunction.Linear;

        for (int i = 0; i < keys.Length; i++)
        {
            ulong h = hashCode(keys[i], seed);
            ulong highHash = HashHelper.Mix64(h ^ 0x9E3779B97F4A7C15UL);
            ulong lowHash = HashHelper.Mix64(h + 0xD6E8FEB86659FD93UL);

            lowHashes[i] = lowHash;

            uint bucket;
            if (linearBuckets)
            {
                bucket = HashHelper.Reduce64(highHash, numBuckets);
            }
            else
            {
                (uint part, ulong remainder) = ReduceWithRemainder(highHash, numParts);
                uint bucketInPart = HashHelper.Reduce64(ApplyBucketFunction(remainder, settings.BucketFunction), bucketsPerPart);
                bucket = (part * bucketsPerPart) + bucketInPart;
            }

            bucketByKey[i] = (int)bucket;
            bucketCounts[(int)bucket]++;
        }

        int maxBucketSize = 0;
        bucketStarts[0] = 0;

        for (int i = 0; i < bucketCounts.Length; i++)
        {
            int count = bucketCounts[i];
            if (count > maxBucketSize)
                maxBucketSize = count;

            bucketStarts[i + 1] = bucketStarts[i] + count;
            bucketOffsets[i] = bucketStarts[i];
            bucketOrder[i] = i;
        }

        buildState.EnsureForCandidates(maxBucketSize);

        int[] candidateSlots = buildState.CandidateSlots;
        int[] bestSlots = buildState.BestSlots;
        int[] collidedBuckets = buildState.CollidedBuckets;
        int[] bestCollidedBuckets = buildState.BestCollidedBuckets;
        int[] slotMarks = buildState.SlotMarks;

        for (int i = 0; i < keys.Length; i++)
        {
            int bucket = bucketByKey[i];
            int offset = bucketOffsets[bucket]++;
            bucketKeyIndices[offset] = i;
        }

        Array.Sort(bucketOrder, (a, b) => bucketCounts[b].CompareTo(bucketCounts[a]));

        for (int i = 0; i < slotOwners.Length; i++)
            slotOwners[i] = -1;
        Array.Clear(pilots, 0, pilots.Length);

        bool failed = false;

        for (int order = 0; order < bucketOrder.Length; order++)
        {
            int bucket = bucketOrder[order];
            int bucketCount = bucketCounts[bucket];

            if (bucketCount == 0)
                continue;

            if (!TryPlaceBucket(
                    bucket,
                    seed,
                    settings,
                    bucketsPerPart,
                    slotsPerPart,
                    slotMultiplier,
                    bucketStarts,
                    bucketCounts,
                    bucketKeyIndices,
                    lowHashes,
                    slotOwners,
                    pilots,
                    candidateSlots,
                    bestSlots,
                    collidedBuckets,
                    bestCollidedBuckets,
                    recentBuckets,
                    slotMarks))
            {
                LogBucketFailure(bucket, bucketCount);
                failed = true;
                break;
            }
        }

        if (failed)
        {
            BuildBucketOwnedCounts(slotOwners, bucketOwnedCounts);

            uint placedKeyCount = 0;
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                if (bucketCounts[i] == bucketOwnedCounts[i])
                    placedKeyCount += (uint)bucketCounts[i];
            }

            for (int i = 0; i < slotOwners.Length; i++)
            {
                int owner = slotOwners[i];

                if (owner < 0)
                    continue;

                if (bucketCounts[owner] != bucketOwnedCounts[owner])
                    slotOwners[i] = -1;
            }

            uint[] partialRemap = BuildRemap(slotOwners, placedKeyCount, numSlots);
            PtrHashMinimalState<TKey> partialState = new PtrHashMinimalState<TKey>(
                placedKeyCount,
                numSlots,
                numParts,
                slotsPerPart,
                bucketsPerPart,
                settings.BucketFunction,
                seed,
                pilots,
                partialRemap,
                hashCode);

            Dictionary<TKey, uint> remainder = new Dictionary<TKey, uint>(keys.Length - (int)placedKeyCount);
            uint remainderIndex = placedKeyCount;

            for (int i = 0; i < keys.Length; i++)
            {
                int bucket = bucketByKey[i];

                if (bucketCounts[bucket] == bucketOwnedCounts[bucket])
                    continue;

                remainder[keys[i]] = remainderIndex;
                remainderIndex++;
            }

            LogFailure();
            result = new PartialBuildResult<TKey, PtrHashMinimalState<TKey>>(partialState, remainder);
            return PartialBuildStatus.Partial;
        }

        uint[] remap = BuildRemap(slotOwners, numKeys, numSlots);
        PtrHashMinimalState<TKey> state = new PtrHashMinimalState<TKey>(
            numKeys,
            numSlots,
            numParts,
            slotsPerPart,
            bucketsPerPart,
            settings.BucketFunction,
            seed,
            pilots,
            remap,
            hashCode);

        result = new PartialBuildResult<TKey, PtrHashMinimalState<TKey>>(state, new Dictionary<TKey, uint>());
        LogSuccess(seed);
        return PartialBuildStatus.Success;
    }

    private static void BuildBucketOwnedCounts(int[] slotOwners, int[] bucketOwnedCounts)
    {
        Array.Clear(bucketOwnedCounts, 0, bucketOwnedCounts.Length);

        for (int i = 0; i < slotOwners.Length; i++)
        {
            int owner = slotOwners[i];

            if (owner >= 0)
                bucketOwnedCounts[owner]++;
        }
    }

    private static bool TryPlaceBucket(
        int initialBucket,
        ulong seed,
        PtrHashMinimalSettings settings,
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
                slotMarks))
            return true;

        if (!settings.EnableEviction)
            return false;

        uint maxEvictions = settings.MaxEvictionsPerChain == 0 ? slotsPerPart * 10u : settings.MaxEvictionsPerChain;
        uint evictionCount = 0;

        PriorityQueue<int, int> queue = new PriorityQueue<int, int>();
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
                    slotMarks))
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
        int[] slotMarks)
    {
        int slotCount = bucketCounts[bucket];
        int markId = 0;

        for (uint pilot = 0; pilot < maxPilot; pilot++)
        {
            int currentMark = NextMarkId(slotMarks, ref markId);

            bool valid = TryEvaluatePilot(
                bucket,
                seed,
                pilot,
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
        int markId = 0;

        for (uint delta = 0; delta < maxPilot; delta++)
        {
            uint pilot = startPilot + delta;
            if (pilot >= maxPilot)
                pilot -= maxPilot;

            int currentMark = NextMarkId(slotMarks, ref markId);

            bool valid = TryEvaluatePilot(
                bucket,
                seed,
                pilot,
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
        uint pilot,
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
        uint pilot,
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

    private static uint ComputeParts(uint numKeys, PtrHashMinimalSettings settings)
    {
        if (settings.Parts > 0)
            return settings.Parts;

        if (numKeys == 0)
            return 1;

        double value = Math.Ceiling(numKeys / (double)settings.TargetKeysPerPart);
        return Math.Max(1u, (uint)value);
    }

    private static (uint Reduced, ulong Remainder) ReduceWithRemainder(ulong hash, uint range)
    {
        uint reduced = (uint)Math.BigMul(hash, range, out ulong remainder);
        return (reduced, remainder);
    }

    private static ulong ComputeFastModMultiplier(uint range) => unchecked((ulong.MaxValue / range) + 1UL);

    private static uint ReduceFastMod32(ulong hash, uint range, ulong multiplier)
    {
        ulong lowBits = unchecked(multiplier * hash);
        return (uint)Math.BigMul(lowBits, range, out _);
    }

    private static ulong PilotMix(uint pilot, ulong seed) => unchecked(0x517CC1B727220A95UL * (pilot ^ seed));

    private static ulong ApplyBucketFunction(ulong hash, PtrHashBucketFunction bucketFunction) => bucketFunction switch
    {
        PtrHashBucketFunction.Linear => hash,
        PtrHashBucketFunction.SquareEps => (Math.BigMul(hash, hash, out _) / 256UL * 255UL) + (hash / 256UL),
        PtrHashBucketFunction.CubicEps => (Math.BigMul(Math.BigMul(hash, hash, out _), (hash >> 1) | (1UL << 63), out _) / 256UL * 255UL) + (hash / 256UL),
        _ => hash
    };

    private static bool IsPowerOfTwo(uint value) => value != 0 && (value & (value - 1)) == 0;

    private sealed class PtrHashBuildState
    {
        public ulong[] LowHashes = [];
        public int[] BucketByKey = [];
        public int[] BucketCounts = [];
        public int[] BucketStarts = [];
        public int[] BucketOffsets = [];
        public int[] BucketKeyIndices = [];
        public int[] BucketOrder = [];
        public int[] SlotOwners = [];
        public int[] BucketOwnedCounts = [];
        public byte[] Pilots = [];
        public int[] RecentBuckets = [];
        public int[] CandidateSlots = [];
        public int[] BestSlots = [];
        public int[] CollidedBuckets = [];
        public int[] BestCollidedBuckets = [];
        public int[] SlotMarks = [];

        public void EnsureForKeys(int numKeys)
        {
            ArrayEnsure.EnsureCapacity(ref LowHashes, numKeys);
            ArrayEnsure.EnsureCapacity(ref BucketByKey, numKeys);
            ArrayEnsure.EnsureCapacity(ref BucketKeyIndices, numKeys);
        }

        public void EnsureForBuckets(int numBuckets)
        {
            ArrayEnsure.EnsureCapacity(ref BucketCounts, numBuckets);
            ArrayEnsure.EnsureCapacity(ref BucketStarts, numBuckets + 1);
            ArrayEnsure.EnsureCapacity(ref BucketOffsets, numBuckets);
            ArrayEnsure.EnsureCapacity(ref BucketOrder, numBuckets);
            ArrayEnsure.EnsureCapacity(ref BucketOwnedCounts, numBuckets);
            ArrayEnsure.EnsureCapacity(ref Pilots, numBuckets);
        }

        public void EnsureForSlots(int numSlots)
        {
            ArrayEnsure.EnsureCapacity(ref SlotOwners, numSlots);
            ArrayEnsure.EnsureCapacity(ref SlotMarks, numSlots);
        }

        public void EnsureForEviction(int recentWindow)
        {
            ArrayEnsure.EnsureCapacity(ref RecentBuckets, recentWindow);
        }

        public void EnsureForCandidates(int maxBucketSize)
        {
            ArrayEnsure.EnsureCapacity(ref CandidateSlots, maxBucketSize);
            ArrayEnsure.EnsureCapacity(ref BestSlots, maxBucketSize);
            ArrayEnsure.EnsureCapacity(ref CollidedBuckets, maxBucketSize);
            ArrayEnsure.EnsureCapacity(ref BestCollidedBuckets, maxBucketSize);
        }
    }
}