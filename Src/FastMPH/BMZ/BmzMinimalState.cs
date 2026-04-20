using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BMZ;

/// <summary>Contains the state of a BMZ minimal perfect hash function</summary>
[PublicAPI]
public sealed class BmzMinimalState<TKey> : IQueryState<TKey> where TKey : notnull
{
    private readonly HashFunc<TKey> _hashFunc;

    internal BmzMinimalState(uint numVertices, ulong seed, uint[] lookupTable, HashFunc<TKey> hashFunc)
    {
        NumVertices = numVertices;
        Seed = seed;
        LookupTable = lookupTable;
        _hashFunc = hashFunc;
    }

    /// <summary>Contains the number of vertices in the graph</summary>
    public uint NumVertices { get; }

    /// <summary>The seed used in the hash function</summary>
    public ulong Seed { get; }

    /// <summary>The lookup table</summary>
    public uint[] LookupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        ulong h = _hashFunc(key, Seed);
        uint h1 = (uint)h % NumVertices;
        uint h2 = (uint)(h >> 32) % NumVertices;

        if (h1 == h2 && ++h2 >= NumVertices)
            h2 = 0;

        return LookupTable[h1] + LookupTable[h2];
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(uint) + //NumVertices
                                   sizeof(ulong) + //Seed
                                   sizeof(uint) + //Length of lookupTable
                                   (sizeof(uint) * (uint)LookupTable.Length); //lookupTable

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt64(Seed);
        sw.WriteUInt32(NumVertices);
        sw.WriteUInt32Array(LookupTable);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BmzMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    public static BmzMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, HashFunc<TKey> hashFunc)
    {
        SpanReader sw = new SpanReader(packed);
        ulong seed = sw.ReadUInt64();
        uint numVertices = sw.ReadUInt32();
        uint[] lookupTable = sw.ReadUInt32Array();

        return new BmzMinimalState<TKey>(numVertices, seed, lookupTable, hashFunc);
    }
}