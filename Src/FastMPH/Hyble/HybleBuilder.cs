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
    public bool TryCreate(ReadOnlySpan<TKey> keys, Func<TKey, uint> hashFunc, [NotNullWhen(true)]out HybleState<TKey>? state, HybleSettings? settings = null)
    {
        settings ??= new HybleSettings();

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(hashFunc);

        state = null;

        // Keep the 32-bit data limitation explicit to match the Rust implementation.
        if (keys.Length > (int.MaxValue / 2))
            return false;

        uint numKeys = (uint)keys.Length;

        if (numKeys == 0)
        {
            state = new HybleState<TKey>(1, 0, [0], hashCode);
            return true;
        }

        if (!TryComputeApproxRange(numKeys, out uint approxRange))
            return false;

        if (!TryComputeBucketLayout(numKeys, settings.KeysPerBucket, out uint bucketCount, out uint bucketMask))
            return false;

        LogCreating(numKeys, approxRange, bucketCount);

        uint[] approxs = new uint[numKeys];
        int[] bucketsByKey = new int[numKeys];
        int[] bucketCounts = new int[bucketCount];
        int[] bucketStarts = new int[bucketCount + 1];
        int[] bucketOffsets = new int[bucketCount];
        int[] bucketKeyIndices = new int[numKeys];
        int[] bucketOrder = new int[bucketCount];
        ushort[] displacements = new ushort[bucketCount];

        ulong bitmapBits = approxRange + (ulong)ushort.MaxValue;
        int bitmapByteLength = (int)((bitmapBits + 7) / 8);
        byte[] freeBitmap = new byte[bitmapByteLength];

        for (uint attempt = 0; attempt < settings.Iterations; attempt++)
        {
            uint seed = RandomHelper.Next();
            LogAttempt(attempt + 1, seed);

            Array.Clear(bucketCounts, 0, bucketCounts.Length);

            for (int i = 0; i < numKeys; i++)
            {
                uint hash = hashCode(keys[i], seed);
                (uint approx, int bucket) = ToApproxBucket(hash, approxRange, bucketMask);
                approxs[i] = approx;
                bucketsByKey[i] = bucket;
                bucketCounts[bucket]++;
            }

            bucketStarts[0] = 0;

            for (int i = 0; i < bucketCount; i++)
            {
                bucketStarts[i + 1] = bucketStarts[i] + bucketCounts[i];
                bucketOffsets[i] = bucketStarts[i];
                bucketOrder[i] = i;
            }

            for (int i = 0; i < numKeys; i++)
            {
                int bucket = bucketsByKey[i];
                int offset = bucketOffsets[bucket]++;
                bucketKeyIndices[offset] = i;
            }

            if (HasApproxCollision(bucketStarts, bucketCounts, bucketKeyIndices, approxs))
                continue;

            Array.Sort(bucketOrder, (a, b) =>
            {
                int sizeCompare = bucketCounts[b].CompareTo(bucketCounts[a]);
                return sizeCompare != 0 ? sizeCompare : a.CompareTo(b);
            });

            Array.Clear(displacements, 0, displacements.Length);
            for (int i = 0; i < freeBitmap.Length; i++)
                freeBitmap[i] = byte.MaxValue;

            bool failed = false;

            for (int order = 0; order < bucketOrder.Length; order++)
            {
                int bucket = bucketOrder[order];
                int size = bucketCounts[bucket];

                if (size == 0)
                    continue;

                if (!TryFindDisplacement(bucket, bucketStarts, bucketCounts, bucketKeyIndices, approxs, freeBitmap, settings.DisplacementSearchStride, out ushort displacement))
                {
                    LogBucketFailure(bucket, size);
                    failed = true;
                    break;
                }

                displacements[bucket] = displacement;
                MarkBucketAsUsed(bucket, bucketStarts, bucketCounts, bucketKeyIndices, approxs, displacement, freeBitmap);
            }

            if (failed)
                continue;

            uint capacity = GetCapacity(approxRange, displacements);
            state = new HybleState<TKey>(approxRange, seed, displacements, hashCode);
            LogSuccess(seed, capacity);
            return true;
        }

        LogFailure();
        state = null;
        return false;
    }

    private static uint GetCapacity(uint approxRange, ushort[] displacements)
    {
        ushort max = 0;

        for (int i = 0; i < displacements.Length; i++)
        {
            if (displacements[i] > max)
                max = displacements[i];
        }

        return approxRange + max;
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

    private static (uint approx, int bucket) ToApproxBucket(uint hash, uint approxRange, uint bucketMask)
    {
        uint approx = HashHelper.Reduce(hash, approxRange);
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
        // Use different load factors for different sizes. This was tuned experimentally. We used to
        // increase `approx_range` after each failure to improve the chances of success, but that
        // doesn't seem necessary.
        uint percent = DivCeil(numKeys, 100);
        uint coeff = Math.Min(DivCeil(numKeys, 1_000_000), 5u);
        ulong approxRange64 = numKeys + ((ulong)coeff * percent);

        if (approxRange64 < numKeys)
        {
            //Hash space too small
            approxRange = 0;
            return false;
        }

        if (approxRange64 > int.MaxValue - ushort.MaxValue)
        {
            //approx_range too large
            approxRange = 0;
            return false;
        }

        approxRange = (uint)approxRange64;
        return true;
    }

    private static uint DivCeil(uint a, uint b) => (a + b - 1) / b;
}