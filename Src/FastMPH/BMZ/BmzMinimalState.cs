using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BMZ;

/// <summary>Contains the state of a BDZ minimal perfect hash function</summary>
[PublicAPI]
public sealed class BmzMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;

    internal BmzMinimalState(uint vertices, uint seed0, uint seed1, uint[] lookupTable, HashCode<TKey> func)
    {
        _hashCode = func;
        Vertices = vertices;
        Seed0 = seed0;
        Seed1 = seed1;
        LookupTable = lookupTable;
    }

    /// <summary>Contains the number of vertices in the graph</summary>
    public uint Vertices { get; }

    /// <summary>The seed used in the first hash function</summary>
    public uint Seed0 { get; }

    /// <summary>The seed used in the second hash function</summary>
    public uint Seed1 { get; }

    /// <summary>The lookup table</summary>
    public uint[] LookupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint h1 = _hashCode(key, Seed0) % Vertices;
        uint h2 = _hashCode(key, Seed1) % Vertices;

        if (h1 == h2 && ++h2 >= Vertices)
            h2 = 0;

        return LookupTable[h1] + LookupTable[h2];
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(uint) + //NumVertices
                                   sizeof(uint) + //Seed0
                                   sizeof(uint) + //Seed1
                                   sizeof(uint) + //Length of lookupTable
                                   (sizeof(uint) * (uint)LookupTable.Length); //lookupTable

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(Vertices);
        sw.WriteUInt32(Seed0);
        sw.WriteUInt32(Seed1);
        sw.WriteUInt32((uint)LookupTable.Length);

        foreach (uint t in LookupTable)
            sw.WriteUInt32(t);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BmzMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function</param>
    public static BmzMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        SpanReader sw = new SpanReader(packed);
        uint numVertices = sw.ReadUInt32();
        uint seed0 = sw.ReadUInt32();
        uint seed1 = sw.ReadUInt32();
        uint length = sw.ReadUInt32();

        uint[] lookupTable = new uint[length];

        for (int i = 0; i < length; i++)
            lookupTable[i] = sw.ReadUInt32();

        return new BmzMinimalState<TKey>(numVertices, seed0, seed1, lookupTable, HashHelper.GetHashFunc(comparer));
    }
}