using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHM;

/// <summary>Contains the state of a CHM minimal perfect hash function</summary>
[PublicAPI]
public sealed class ChmMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;

    internal ChmMinimalState(uint numVertices, uint numEdges, uint[] lookupTable, uint seed0, uint seed1, HashCode<TKey> hashCode)
    {
        _hashCode = hashCode;
        NumVertices = numVertices;
        NumEdges = numEdges;
        LookupTable = lookupTable;
        Seed0 = seed0;
        Seed1 = seed1;
    }

    /// <summary>The number of vertices in the graph</summary>
    public uint NumVertices { get; }

    /// <summary>The number of edges in the graph</summary>
    public uint NumEdges { get; }

    /// <summary>The seed used in the first hash function</summary>
    public uint Seed0 { get; }

    /// <summary>The seed used in the second hash function</summary>
    public uint Seed1 { get; }

    /// <summary>The lookup table</summary>
    public uint[] LookupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint h1 = _hashCode(key, Seed0) % NumVertices;
        uint h2 = _hashCode(key, Seed1) % NumVertices;

        if (h1 == h2 && ++h2 >= NumVertices)
            h2 = 0;

        return (LookupTable[h1] + LookupTable[h2]) % NumEdges;
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(uint) + //NumVertices
                                   sizeof(uint) + //NumEdges
                                   sizeof(uint) + //Seed0
                                   sizeof(uint) + //Seed1
                                   sizeof(uint) + //Length of lookupTable
                                   (sizeof(uint) * (uint)LookupTable.Length); //lookupTable

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(NumVertices);
        sw.WriteUInt32(NumEdges);
        sw.WriteUInt32(Seed0);
        sw.WriteUInt32(Seed1);
        sw.WriteUInt32((uint)LookupTable.Length);

        foreach (uint t in LookupTable)
            sw.WriteUInt32(t);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="ChmMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function</param>
    public static ChmMinimalState<TKey> Unpack(Span<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        SpanReader sw = new SpanReader(packed);
        uint numVertices = sw.ReadUInt32();
        uint numEdges = sw.ReadUInt32();
        uint seed0 = sw.ReadUInt32();
        uint seed1 = sw.ReadUInt32();
        uint length = sw.ReadUInt32();

        uint[] lookupTable = new uint[length];

        for (int i = 0; i < length; i++)
            lookupTable[i] = sw.ReadUInt32();

        return new ChmMinimalState<TKey>(numVertices, numEdges, lookupTable, seed0, seed1, HashHelper.GetHashFunc(comparer));
    }
}