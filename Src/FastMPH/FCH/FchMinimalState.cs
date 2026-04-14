using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.FCH;

/// <summary>Contains the state of a FCH minimal perfect hash function</summary>
[PublicAPI]
public sealed class FchMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode<TKey> _hashCode;

    internal FchMinimalState(uint numItems, uint b, double p1, double p2, ulong seed0, ulong seed1, uint[] lookupTable, HashCode<TKey> hashCode)
    {
        _hashCode = hashCode;
        NumItems = numItems;
        B = b;
        P1 = p1;
        P2 = p2;
        LookupTable = lookupTable;
        Seed0 = seed0;
        Seed1 = seed1;
    }

    /// <summary>The number of items in the hash function</summary>
    public uint NumItems { get; }
    public uint B { get; }
    public double P1 { get; }
    public double P2 { get; }

    /// <summary>The mapping seed</summary>
    public ulong Seed0 { get; }

    /// <summary>The search seed</summary>
    public ulong Seed1 { get; }

    /// <summary>The lookup table</summary>
    public uint[] LookupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint h1 = (uint)(HashHelper.Mix64(_hashCode(key, Seed0)) % NumItems);
        uint h2 = (uint)(HashHelper.Mix64(_hashCode(key, Seed1)) % NumItems);
        h1 = FchBuilder<TKey>.Mixh10h11h12(B, P1, P2, h1);
        return (h2 + LookupTable[h1]) % NumItems;
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(uint) + //NumItems
                                   sizeof(uint) + //B
                                   sizeof(double) + //P1
                                   sizeof(double) + //P2
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
        sw.WriteDouble(P1);
        sw.WriteDouble(P2);
        sw.WriteUInt64(Seed0);
        sw.WriteUInt64(Seed1);
        sw.WriteUInt32Array(LookupTable);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="FchMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="hashFunc">The hash function that was used when creating the hash function.</param>
    public static FchMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, Func<TKey, ulong> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        uint numItems = sr.ReadUInt32();
        uint b = sr.ReadUInt32();
        double p1 = sr.ReadDouble();
        double p2 = sr.ReadDouble();
        ulong seed0 = sr.ReadUInt64();
        ulong seed1 = sr.ReadUInt64();
        uint[] lookupTable = sr.ReadUInt32Array();

        return new FchMinimalState<TKey>(numItems, b, p1, p2, seed0, seed1, lookupTable, HashHelper.GetHashFunc(hashFunc));
    }
}
