using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.FCH;

/// <summary>Contains the state of a FCH minimal perfect hash function</summary>
[PublicAPI]
public sealed class FchMinimalState<TKey> : IQueryState<TKey> where TKey : notnull
{
    private readonly HashFunc<TKey> _hashFunc;

    internal FchMinimalState(uint numItems, uint b, uint p1, uint p2, ulong seed0, ulong seed1, uint[] lookupTable, HashFunc<TKey> hashFunc)
    {
        NumItems = numItems;
        B = b;
        P1 = p1;
        P2 = p2;
        LookupTable = lookupTable;
        Seed0 = seed0;
        Seed1 = seed1;
        _hashFunc = hashFunc;
    }

    /// <summary>The number of items in the hash function</summary>
    public uint NumItems { get; }

    public uint B { get; }
    public uint P1 { get; }
    public uint P2 { get; }

    /// <summary>The mapping seed</summary>
    public ulong Seed0 { get; }

    /// <summary>The search seed</summary>
    public ulong Seed1 { get; }

    /// <summary>The lookup table</summary>
    public uint[] LookupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint h1 = (uint)(HashHelper.Mix64(_hashFunc(key, Seed0)) % NumItems);
        uint h2 = (uint)(HashHelper.Mix64(_hashFunc(key, Seed1)) % NumItems);
        h1 = FchBuilder<TKey>.Mixh10h11h12(B, P1, P2, h1);
        return (h2 + LookupTable[h1]) % NumItems;
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(uint) + //NumItems
                                   sizeof(uint) + //B
                                   sizeof(uint) + //P1
                                   sizeof(uint) + //P2
                                   sizeof(ulong) + //Seed0
                                   sizeof(ulong) + //Seed1
                                   sizeof(uint) + //LookupTable length
                                   (sizeof(uint) * (uint)LookupTable.Length); //LookupTable

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(NumItems);
        sw.WriteUInt32(B);
        sw.WriteUInt32(P1);
        sw.WriteUInt32(P2);
        sw.WriteUInt64(Seed0);
        sw.WriteUInt64(Seed1);
        sw.WriteUInt32Array(LookupTable);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="FchMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    public static FchMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, HashFunc<TKey> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        uint numItems = sr.ReadUInt32();
        uint b = sr.ReadUInt32();
        uint p1 = sr.ReadUInt32();
        uint p2 = sr.ReadUInt32();
        ulong seed0 = sr.ReadUInt64();
        ulong seed1 = sr.ReadUInt64();
        uint[] lookupTable = sr.ReadUInt32Array();

        return new FchMinimalState<TKey>(numItems, b, p1, p2, seed0, seed1, lookupTable, hashFunc);
    }
}