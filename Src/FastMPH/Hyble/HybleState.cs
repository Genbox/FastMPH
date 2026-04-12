using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.Hyble;

/// <summary>Contains the state of a Hyble perfect hash function.</summary>
[PublicAPI]
public sealed class HybleState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;
    private readonly uint _bucketMask;

    internal HybleState(uint approxRange, ulong seed, ushort[] displacements, HashCode<TKey> hashCode)
    {
        ApproxRange = approxRange;
        Seed = seed;
        Displacements = displacements;
        _bucketMask = (uint)displacements.Length - 1;
        _hashCode = hashCode;
    }

    /// <summary>Upper bound used for the approximate range.</summary>
    public uint ApproxRange { get; }

    /// <summary>Construction seed.</summary>
    public ulong Seed { get; }

    /// <summary>Per-bucket displacement values.</summary>
    public ushort[] Displacements { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        ulong hash = _hashCode(key, Seed);
        return SearchHash(hash);
    }

    /// <summary>
    /// Compute the index from a pre-hashed value.
    /// </summary>
    /// <param name="hash">Hash of the key. Only the lower 32 bits are used.</param>
    /// <summary>
    /// Compute the index from a pre-hashed value.
    /// </summary>
    /// <param name="hash">Hash of the key.</param>
    public uint SearchHash(ulong hash)
    {
        uint approx = HashHelper.Reduce64(hash, ApproxRange);
        int bucket = (int)(hash & _bucketMask);

        return approx + Displacements[bucket];
    }

    /// <summary>
    /// Get packed size for the format:
    /// <c>[ApproxRange:u32][Seed:u32][DisplacementsLength:u32][Displacements:u16[]]</c>.
    /// </summary>
    public uint GetPackedSize() => sizeof(uint) + // ApproxRange
                                   sizeof(ulong) + // Seed
                                   sizeof(uint) + // Displacements length
                                   ((uint)Displacements.Length * sizeof(ushort));

    /// <summary>
    /// Serialize this state using the packed format documented by <see cref="GetPackedSize"/>.
    /// </summary>
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(ApproxRange);
        sw.WriteUInt64(Seed);
        sw.WriteUInt32((uint)Displacements.Length);

        foreach (ushort displacement in Displacements)
            sw.WriteUInt16(displacement);
    }

    /// <summary>
    /// Deserialize a serialized perfect hash function into a new instance of <see cref="HybleState{TKey}" />.
    /// </summary>
    /// <param name="packed">The serialized hash function.</param>
    /// <param name="hashFunc">The hash function that was used when creating the hash function.</param>
    public static HybleState<TKey> Unpack(ReadOnlySpan<byte> packed, Func<TKey, ulong> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        uint approxRange = sr.ReadUInt32();
        ulong seed = sr.ReadUInt64();
        int displacementLength = (int)sr.ReadUInt32();
        ushort[] displacements = new ushort[displacementLength];

        for (int i = 0; i < displacements.Length; i++)
            displacements[i] = sr.ReadUInt16();

        if (!IsValidState(displacements))
            throw new InvalidOperationException("Packed Hyble state invariants are invalid");

        return new HybleState<TKey>(approxRange, seed, displacements, HashHelper.GetHashFunc(hashFunc));
    }

    private static bool IsValidState(ushort[] displacements)
    {
        int bucketCount = displacements.Length;
        return bucketCount > 0 && (bucketCount & (bucketCount - 1)) == 0;
    }
}