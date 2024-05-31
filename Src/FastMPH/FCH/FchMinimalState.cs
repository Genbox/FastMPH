using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.FCH;

/// <summary>Contains the state of a FCH minimal perfect hash function</summary>
[PublicAPI]
public sealed class FchMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;

    internal FchMinimalState(uint items, uint b, double p1, double p2, uint seed0, uint seed1, uint[] lookupTable, HashCode<TKey> hashCode)
    {
        _hashCode = hashCode;
        Items = items;
        B = b;
        P1 = p1;
        P2 = p2;
        LookupTable = lookupTable;
        Seed0 = seed0;
        Seed1 = seed1;
    }

    /// <summary>
    /// The number of items in the hash function
    /// </summary>
    public uint Items { get; }

    public uint B { get; }
    public double P1 { get; }
    public double P2 { get; }

    /// <summary>The seed for the first hash function</summary>
    public uint Seed0 { get; }

    /// <summary>The seed for the second hash function</summary>
    public uint Seed1 { get; }

    /// <summary>The lookup table</summary>
    public uint[] LookupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint h1 = _hashCode(key, Seed0) % Items;
        uint h2 = _hashCode(key, Seed1) % Items;

        h1 = FchBuilder<TKey>.Mixh10h11h12(B, P1, P2, h1);
        return (h2 + LookupTable[h1]) % Items;
    }

    /// <inheritdoc />
    public uint GetPackedSize()
    {
        return sizeof(uint) + //NumItems
               sizeof(uint) + //B
               sizeof(double) + //P1
               sizeof(double) + //P2
               sizeof(uint) + //Seed1
               sizeof(uint) + //Seed2
               sizeof(uint) + // LookupTable length
               sizeof(uint) * (uint)LookupTable.Length; //LookupTable
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(Items);
        sw.WriteUInt32(B);
        sw.WriteDouble(P1);
        sw.WriteDouble(P2);
        sw.WriteUInt32(Seed0);
        sw.WriteUInt32(Seed1);
        sw.WriteUInt32((uint)LookupTable.Length);

        foreach (uint t in LookupTable)
            sw.WriteUInt32(t);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="FchMinimalState{TKey}"/>
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function</param>
    public static FchMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        SpanReader sr = new SpanReader(packed);
        uint numItems = sr.ReadUInt32();
        uint b = sr.ReadUInt32();
        double p1 = sr.ReadDouble();
        double p2 = sr.ReadDouble();
        uint seed0 = sr.ReadUInt32();
        uint seed1 = sr.ReadUInt32();
        uint length = sr.ReadUInt32();

        uint[] lookupTable = new uint[length];

        for (int i = 0; i < length; i++)
            lookupTable[i] = sr.ReadUInt32();

        return new FchMinimalState<TKey>(numItems, b, p1, p2, seed0, seed1, lookupTable, HashHelper.GetHashFunc(comparer));
    }
}