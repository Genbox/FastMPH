using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BMZ;

/// <summary>Contains the state of a BDZ minimal perfect hash function</summary>
[PublicAPI]
public sealed class BmzMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;

    internal BmzMinimalState(uint numVertices, ulong seed, uint[] lookupTable, HashCode<TKey> func)
    {
        _hashCode = func;
        NumVertices = numVertices;
        Seed = seed;
        LookupTable = lookupTable;
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
        ulong h = _hashCode(key, Seed);
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
        sw.WriteUInt32(NumVertices);
        sw.WriteUInt64(Seed);
        sw.WriteUInt32((uint)LookupTable.Length);

        foreach (uint t in LookupTable)
            sw.WriteUInt32(t);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BmzMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="hashFunc">The hash function that was used when creating the hash function.</param>
    public static BmzMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, Func<TKey, ulong> hashFunc)
    {
        SpanReader sw = new SpanReader(packed);
        uint numVertices = sw.ReadUInt32();
        ulong seed = sw.ReadUInt64();
        uint length = sw.ReadUInt32();

        uint[] lookupTable = new uint[length];

        for (int i = 0; i < length; i++)
            lookupTable[i] = sw.ReadUInt32();

        return new BmzMinimalState<TKey>(numVertices, seed, lookupTable, HashHelper.GetHashFunc(hashFunc));
    }
}