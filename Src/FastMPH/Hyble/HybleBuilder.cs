using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.Hyble;

/// <summary>
/// Hyble is a displacement-based perfect hash algorithm that assigns per-bucket offsets over an approximate range.
///
/// This implementation assumes full-avalanche hash quality and uses a 32-bit seeded hash pipeline.
/// </summary>
[PublicAPI]
public sealed partial class HybleBuilder<TKey> : IHashBuilder<TKey, HybleState<TKey>, HybleSettings> where TKey : notnull
{
    private const uint MaxDisplacementBase = ushort.MaxValue - 64;

    /// <inheritdoc />
    public bool TryCreate(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out HybleState<TKey>? state, HybleSettings? settings = null)
    {
        settings ??= new HybleSettings();

        if (!TryCreateState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateState(int numKeys, HybleSettings settings, [NotNullWhen(true)]out IBuildState? state)
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
    public bool TryCreateCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, HybleSettings settings, [NotNullWhen(true)]out HybleState<TKey>? queryState)
    {
        if (keys.Length == 0)
        {
            queryState = new HybleState<TKey>(1, 0, [0], hashFunc);
            return true;
        }

        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        LogCreating(keys.Length, build.ApproxRange, build.BucketCount);

        Array.Clear(build.BucketCounts, 0, build.BucketCounts.Length);

        for (int i = 0; i < keys.Length; i++)
        {
            ulong hash = hashFunc(keys[i], seed);
            (uint approx, int bucket) = ToApproxBucket(hash, build.ApproxRange, build.BucketMask);
            build.Approxs[i] = approx;
            build.BucketsByKey[i] = bucket;
            build.BucketCounts[bucket]++;
        }

        build.BucketStarts[0] = 0;

        for (int i = 0; i < build.BucketCount; i++)
        {
            build.BucketStarts[i + 1] = build.BucketStarts[i] + build.BucketCounts[i];
            build.BucketOffsets[i] = build.BucketStarts[i];
            build.BucketOrder[i] = i;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            int bucket = build.BucketsByKey[i];
            int offset = build.BucketOffsets[bucket]++;
            build.BucketKeyIndices[offset] = i;
        }

        if (HasApproxCollision(build.BucketStarts, build.BucketCounts, build.BucketKeyIndices, build.Approxs))
        {
            LogFailure();
            queryState = null;
            return false;
        }

        Array.Sort(build.BucketOrder, (a, b) =>
        {
            int sizeCompare = build.BucketCounts[b].CompareTo(build.BucketCounts[a]);
            return sizeCompare != 0 ? sizeCompare : a.CompareTo(b);
        });

        Array.Clear(build.Displacements, 0, build.Displacements.Length);
        Array.Clear(build.PlacedBuckets, 0, build.PlacedBuckets.Length);
        for (int i = 0; i < build.FreeBitmap.Length; i++)
            build.FreeBitmap[i] = byte.MaxValue;

        for (int order = 0; order < build.BucketOrder.Length; order++)
        {
            int bucket = build.BucketOrder[order];
            int size = build.BucketCounts[bucket];

            if (size == 0)
                continue;

            if (!TryFindDisplacement(bucket, build.BucketStarts, build.BucketCounts, build.BucketKeyIndices, build.Approxs, build.FreeBitmap, settings.DisplacementSearchStride, out ushort displacement))
            {
                LogBucketFailure(bucket, size);
                LogFailure();
                queryState = null;
                return false;
            }

            build.Displacements[bucket] = displacement;
            build.PlacedBuckets[bucket] = 1;
            MarkBucketAsUsed(bucket, build.BucketStarts, build.BucketCounts, build.BucketKeyIndices, build.Approxs, displacement, build.FreeBitmap);
        }

        ushort[] displacements = GC.AllocateUninitializedArray<ushort>(build.Displacements.Length);
        Array.Copy(build.Displacements, displacements, displacements.Length);

        queryState = new HybleState<TKey>(build.ApproxRange, seed, displacements, hashFunc);
        LogSuccess(seed, displacements.Length - 1);
        return true;
    }

    private static bool HasApproxCollision(int[] bucketStarts, int[] bucketCounts, int[] bucketKeyIndices, uint[] approxs)
    {
        for (int bucket = 0; bucket < bucketCounts.Length; bucket++)
        {
            int start = bucketStarts[bucket];
            int size = bucketCounts[bucket];

            for (int i = 1; i < size; i++)
            {
                uint approx = approxs[bucketKeyIndices[start + i]];

                for (int j = 0; j < i; j++)
                {
                    if (approxs[bucketKeyIndices[start + j]] == approx)
                        return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindDisplacement(int bucket, int[] bucketStarts, int[] bucketCounts, int[] bucketKeyIndices, uint[] approxs, byte[] freeBitmap, uint stride, out ushort displacement)
    {
        int start = bucketStarts[bucket];
        int size = bucketCounts[bucket];

        for (uint displacementBase = 0; displacementBase <= MaxDisplacementBase; displacementBase += stride)
        {
            ulong globalFreeMask = ulong.MaxValue;

            for (int i = 0; i < size; i++)
            {
                int keyIndex = bucketKeyIndices[start + i];
                int bitIndex = checked((int)(approxs[keyIndex] + displacementBase));
                globalFreeMask &= ReadMask(freeBitmap, bitIndex);

                if (globalFreeMask == 0)
                    break;
            }

            if (globalFreeMask == 0)
                continue;

            displacement = (ushort)(displacementBase + (uint)BitOperations.TrailingZeroCount(globalFreeMask));
            return true;
        }

        displacement = 0;
        return false;
    }

    private static void MarkBucketAsUsed(int bucket, int[] bucketStarts, int[] bucketCounts, int[] bucketKeyIndices, uint[] approxs, ushort displacement, byte[] freeBitmap)
    {
        int start = bucketStarts[bucket];
        int size = bucketCounts[bucket];

        for (int i = 0; i < size; i++)
        {
            int keyIndex = bucketKeyIndices[start + i];
            int index = checked((int)(approxs[keyIndex] + displacement));
            ResetBit(freeBitmap, index);
        }
    }

    private static (uint approx, int bucket) ToApproxBucket(ulong hash, uint approxRange, uint bucketMask)
    {
        uint approx = HashHelper.Reduce64(hash, approxRange);
        int bucket = (int)(hash & bucketMask);
        return (approx, bucket);
    }

    private static ulong ReadMask(byte[] bitmap, int bitIndex)
    {
        int byteIndex = bitIndex >> 3;

        if (byteIndex > bitmap.Length - sizeof(ulong))
            return 0;

        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(bitmap.AsSpan(byteIndex, sizeof(ulong)));
        return value >> (bitIndex & 7);
    }

    private static void ResetBit(byte[] bitmap, int index) => bitmap[index >> 3] &= (byte)~(1 << (index & 7));

    private static bool TryComputeBucketLayout(uint numKeys, uint keysPerBucket, out uint bucketCount, out uint bucketMask)
    {
        uint requested = DivCeil(numKeys, keysPerBucket);
        bucketCount = BitOperations.RoundUpToPowerOf2(requested);

        if (bucketCount == 0 || bucketCount < requested || bucketCount > int.MaxValue)
        {
            bucketMask = 0;
            return false;
        }

        bucketMask = bucketCount - 1;
        return true;
    }

    private static bool TryComputeApproxRange(uint numKeys, out uint approxRange)
    {
        uint percent = DivCeil(numKeys, 100);
        uint coeff = Math.Min(DivCeil(numKeys, 1_000_000), 5u);
        ulong approxRange64 = numKeys + ((ulong)coeff * percent);

        if (approxRange64 < numKeys)
        {
            approxRange = 0;
            return false;
        }

        if (approxRange64 > int.MaxValue - ushort.MaxValue)
        {
            approxRange = 0;
            return false;
        }

        approxRange = (uint)approxRange64;
        return true;
    }

    private static uint DivCeil(uint a, uint b) => (a + b - 1) / b;

    private sealed class BuildState : IBuildState
    {
        public uint ApproxRange;
        public uint BucketCount;
        public uint BucketMask;
        public uint[] Approxs = [];
        public int[] BucketsByKey = [];
        public int[] BucketCounts = [];
        public int[] BucketStarts = [];
        public int[] BucketOffsets = [];
        public int[] BucketKeyIndices = [];
        public int[] BucketOrder = [];
        public byte[] PlacedBuckets = [];
        public ushort[] Displacements = [];
        public byte[] FreeBitmap = [];

        public static bool TryCreate(int keysLength, HybleSettings settings, [NotNullWhen(true)]out BuildState? state)
        {
            state = null;

            if (keysLength > int.MaxValue / 2)
                return false;

            uint numKeys = (uint)keysLength;

            if (numKeys == 0)
            {
                ulong emptyBitmapBits = 1 + (ulong)ushort.MaxValue;
                int emptyBitmapByteLength = (int)((emptyBitmapBits + 7) / 8);

                state = new BuildState
                {
                    ApproxRange = 1,
                    BucketCount = 1,
                    BucketMask = 0,
                    Approxs = [],
                    BucketsByKey = [],
                    BucketKeyIndices = [],
                    BucketCounts = GC.AllocateUninitializedArray<int>(1),
                    BucketStarts = GC.AllocateUninitializedArray<int>(2),
                    BucketOffsets = GC.AllocateUninitializedArray<int>(1),
                    BucketOrder = GC.AllocateUninitializedArray<int>(1),
                    PlacedBuckets = GC.AllocateUninitializedArray<byte>(1),
                    Displacements = GC.AllocateUninitializedArray<ushort>(1),
                    FreeBitmap = GC.AllocateUninitializedArray<byte>(emptyBitmapByteLength)
                };

                return true;
            }

            if (!TryComputeApproxRange(numKeys, out uint approxRange))
                return false;

            if (!TryComputeBucketLayout(numKeys, settings.KeysPerBucket, out uint bucketCount, out uint bucketMask))
                return false;

            ulong bitmapBits = approxRange + (ulong)ushort.MaxValue;
            int bitmapByteLength = (int)((bitmapBits + 7) / 8);

            state = new BuildState
            {
                ApproxRange = approxRange,
                BucketCount = bucketCount,
                BucketMask = bucketMask,
                Approxs = GC.AllocateUninitializedArray<uint>((int)numKeys),
                BucketsByKey = GC.AllocateUninitializedArray<int>((int)numKeys),
                BucketKeyIndices = GC.AllocateUninitializedArray<int>((int)numKeys),
                BucketCounts = GC.AllocateUninitializedArray<int>((int)bucketCount),
                BucketStarts = GC.AllocateUninitializedArray<int>((int)bucketCount + 1),
                BucketOffsets = GC.AllocateUninitializedArray<int>((int)bucketCount),
                BucketOrder = GC.AllocateUninitializedArray<int>((int)bucketCount),
                PlacedBuckets = GC.AllocateUninitializedArray<byte>((int)bucketCount),
                Displacements = GC.AllocateUninitializedArray<ushort>((int)bucketCount),
                FreeBitmap = GC.AllocateUninitializedArray<byte>(bitmapByteLength)
            };

            return true;
        }

        public void Reset()
        {
            Array.Clear(BucketCounts, 0, BucketCounts.Length);
            Array.Clear(BucketOffsets, 0, BucketOffsets.Length);
            Array.Clear(PlacedBuckets, 0, PlacedBuckets.Length);
        }
    }
}