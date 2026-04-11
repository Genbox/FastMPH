using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.PTRHash;

/// <summary>Contains the state of a PTRHash-style minimal perfect hash function.</summary>
[PublicAPI]
public sealed class PtrHashMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;
    private readonly ulong _slotMultiplier;

    internal PtrHashMinimalState(
        uint numKeys,
        uint numSlots,
        uint numParts,
        uint slotsPerPart,
        uint bucketsPerPart,
        PtrHashBucketFunction bucketFunction,
        uint seed,
        byte[] pilots,
        uint[] remap,
        HashCode<TKey> hashCode)
    {
        NumKeys = numKeys;
        NumSlots = numSlots;
        NumParts = numParts;
        SlotsPerPart = slotsPerPart;
        BucketsPerPart = bucketsPerPart;
        BucketFunction = bucketFunction;
        Seed = seed;
        Pilots = pilots;
        Remap = remap;
        _hashCode = hashCode;
        _slotMultiplier = ComputeFastModMultiplier(slotsPerPart);
    }

    /// <summary>The number of keys in the original set.</summary>
    public uint NumKeys { get; }

    /// <summary>Total number of slots used by the function.</summary>
    public uint NumSlots { get; }

    /// <summary>Number of partition parts used during construction.</summary>
    public uint NumParts { get; }

    /// <summary>Number of slots in each part.</summary>
    public uint SlotsPerPart { get; }

    /// <summary>Number of buckets in each part.</summary>
    public uint BucketsPerPart { get; }

    /// <summary>Bucket mapping function used for bucket selection.</summary>
    public PtrHashBucketFunction BucketFunction { get; }

    /// <summary>Construction seed.</summary>
    public uint Seed { get; }

    /// <summary>The per-bucket pilot values.</summary>
    public byte[] Pilots { get; }

    /// <summary>Mapping from overflow slots back into [0,n).</summary>
    public uint[] Remap { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        if (Pilots.Length == 0)
            return 0;

        ulong h = _hashCode(key, Seed);
        ulong highHash = Mix64(h ^ 0x9E3779B97F4A7C15UL);

        uint part;
        uint bucket;
        if (BucketFunction == PtrHashBucketFunction.Linear)
        {
            bucket = Reduce(highHash, (uint)Pilots.Length);
            part = bucket / BucketsPerPart;
        }
        else
        {
            (part, ulong remainder) = ReduceWithRemainder(highHash, NumParts);
            uint bucketInPart = Reduce(ApplyBucketFunction(remainder, BucketFunction), BucketsPerPart);
            bucket = (part * BucketsPerPart) + bucketInPart;
        }

        byte pilot = Pilots[bucket];

        uint slotInPart = ReduceFastMod32(Mix64(h + 0xD6E8FEB86659FD93UL) ^ PilotMix(pilot, Seed), SlotsPerPart, _slotMultiplier);
        uint slot = (part * SlotsPerPart) + slotInPart;

        if (slot < NumKeys)
            return slot;

        uint index = slot - NumKeys;

        if (index < (uint)Remap.Length)
            return Remap[index];

        return 0;
    }

    /// <inheritdoc />
    public uint GetPackedSize()
    {
        return sizeof(uint) + // NumKeys
               sizeof(uint) + // NumSlots
               sizeof(uint) + // NumParts
               sizeof(uint) + // SlotsPerPart
               sizeof(uint) + // BucketsPerPart
               sizeof(byte) + // BucketFunction
               sizeof(uint) + // Seed
               sizeof(uint) + // Pilots length
               (uint)Pilots.Length +
               sizeof(uint) + // Remap length
               (sizeof(uint) * (uint)Remap.Length);
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(NumKeys);
        sw.WriteUInt32(NumSlots);
        sw.WriteUInt32(NumParts);
        sw.WriteUInt32(SlotsPerPart);
        sw.WriteUInt32(BucketsPerPart);
        sw.WriteByte((byte)BucketFunction);
        sw.WriteUInt32(Seed);
        sw.WriteUInt32((uint)Pilots.Length);

        foreach (byte pilot in Pilots)
            sw.WriteByte(pilot);

        sw.WriteUInt32((uint)Remap.Length);

        foreach (uint value in Remap)
            sw.WriteUInt32(value);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="PtrHashMinimalState{TKey}" />.
    /// </summary>
    /// <param name="packed">The serialized hash function.</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function.</param>
    public static PtrHashMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        if (TryUnpackCurrent(packed, out uint numKeys, out uint numSlots, out uint numParts, out uint slotsPerPart, out uint bucketsPerPart, out PtrHashBucketFunction bucketFunction, out uint seed, out byte[] pilots, out uint[] remap))
            return new PtrHashMinimalState<TKey>(numKeys, numSlots, numParts, slotsPerPart, bucketsPerPart, bucketFunction, seed, pilots, remap, HashHelper.GetHashFunc(comparer));

        throw new InvalidOperationException("Invalid packed PTRHash state format");
    }

    private static bool TryUnpackCurrent(
        ReadOnlySpan<byte> packed,
        out uint numKeys,
        out uint numSlots,
        out uint numParts,
        out uint slotsPerPart,
        out uint bucketsPerPart,
        out PtrHashBucketFunction bucketFunction,
        out uint seed,
        [NotNullWhen(true)] out byte[]? pilots,
        [NotNullWhen(true)] out uint[]? remap)
    {
        numKeys = 0;
        numSlots = 0;
        numParts = 0;
        slotsPerPart = 0;
        bucketsPerPart = 0;
        bucketFunction = PtrHashBucketFunction.Linear;
        seed = 0;
        pilots = null;
        remap = null;

        int offset = 0;

        if (!TryReadUInt32(packed, ref offset, out numKeys) ||
            !TryReadUInt32(packed, ref offset, out numSlots) ||
            !TryReadUInt32(packed, ref offset, out numParts) ||
            !TryReadUInt32(packed, ref offset, out slotsPerPart) ||
            !TryReadUInt32(packed, ref offset, out bucketsPerPart) ||
            !TryReadByte(packed, ref offset, out byte bucketFunctionRaw) ||
            !TryReadUInt32(packed, ref offset, out seed) ||
            !TryReadUInt32(packed, ref offset, out uint pilotsLength))
            return false;

        if (bucketFunctionRaw > (byte)PtrHashBucketFunction.CubicEps ||
            numParts == 0 ||
            slotsPerPart == 0 ||
            bucketsPerPart == 0 ||
            pilotsLength > int.MaxValue)
            return false;

        bucketFunction = (PtrHashBucketFunction)bucketFunctionRaw;
        pilots = new byte[pilotsLength];

        for (int i = 0; i < pilots.Length; i++)
        {
            if (!TryReadByte(packed, ref offset, out byte pilot))
                return false;

            pilots[i] = pilot;
        }

        if (!TryReadUInt32(packed, ref offset, out uint remapLength) || remapLength > int.MaxValue)
            return false;

        remap = new uint[remapLength];

        for (int i = 0; i < remap.Length; i++)
        {
            if (!TryReadUInt32(packed, ref offset, out uint value))
                return false;

            remap[i] = value;
        }

        if (offset != packed.Length)
            return false;

        if ((ulong)numParts * slotsPerPart != numSlots)
            return false;

        if ((ulong)numParts * bucketsPerPart != pilotsLength)
            return false;

        return true;
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> packed, ref int offset, out uint value)
    {
        if ((uint)offset > (uint)(packed.Length - sizeof(uint)))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(packed[offset..]);
        offset += sizeof(uint);
        return true;
    }

    private static bool TryReadByte(ReadOnlySpan<byte> packed, ref int offset, out byte value)
    {
        if ((uint)offset >= (uint)packed.Length)
        {
            value = 0;
            return false;
        }

        value = packed[offset];
        offset++;
        return true;
    }

    private static uint Reduce(ulong hash, uint range) => (uint)Math.BigMul(hash, range, out _);

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

    private static ulong PilotMix(byte pilot, uint seed) => unchecked(0x517CC1B727220A95UL * ((ulong)pilot ^ seed));

    private static ulong ApplyBucketFunction(ulong hash, PtrHashBucketFunction bucketFunction)
    {
        return bucketFunction switch
        {
            PtrHashBucketFunction.Linear => hash,
            PtrHashBucketFunction.SquareEps => (Math.BigMul(hash, hash, out _) / 256UL * 255UL) + (hash / 256UL),
            PtrHashBucketFunction.CubicEps => (Math.BigMul(Math.BigMul(hash, hash, out _), (hash >> 1) | (1UL << 63), out _) / 256UL * 255UL) + (hash / 256UL),
            _ => hash
        };
    }

    private static ulong Mix64(ulong x)
    {
        unchecked
        {
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            return x;
        }
    }

}
