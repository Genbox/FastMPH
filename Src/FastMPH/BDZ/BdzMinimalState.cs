using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;
using static Genbox.FastMPH.Internals.BitArray;

namespace Genbox.FastMPH.BDZ;

/// <summary>Contains the state of a BDZ minimal perfect hash function</summary>
[PublicAPI]
public sealed class BdzMinimalState<TKey> : IQueryState<TKey> where TKey : notnull
{
    private readonly HashCode3<TKey> _hashCode;

    internal BdzMinimalState(ulong seed, uint numPartitions, byte bitsOfKey, byte[] lookupTable, uint[] rankTable, HashFunc<TKey> hashFunc)
    {
        Seed = seed;
        NumPartitions = numPartitions;
        BitsOfKey = bitsOfKey;
        LookupTable = lookupTable;
        RankTable = rankTable;
        _hashCode = HashHelper.GetHashFunc3(hashFunc);
    }

    /// <summary>The number of partitions</summary>
    public uint NumPartitions { get; }

    /// <summary>The lookup table</summary>
    public byte[] LookupTable { get; }

    /// <summary>The seed that was used for the hash function</summary>
    public ulong Seed { get; }

    /// <summary>The number of bits per key used for the rank table.</summary>
    public byte BitsOfKey { get; }

    /// <summary>The rank table</summary>
    public uint[] RankTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        Span<uint> hashes = stackalloc uint[3];
        _hashCode(key, Seed, hashes);

        hashes[0] = hashes[0] % NumPartitions;
        hashes[1] = (hashes[1] % NumPartitions) + NumPartitions;
        hashes[2] = (hashes[2] % NumPartitions) + (NumPartitions << 1); // n + n * 2

        uint vertex = hashes[(GetValue(LookupTable, hashes[0]) + GetValue(LookupTable, hashes[1]) + GetValue(LookupTable, hashes[2])) % 3];
        return Rank(BitsOfKey, RankTable, LookupTable, vertex);
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt64(Seed);
        sw.WriteUInt32(NumPartitions);
        sw.WriteUInt32Array(RankTable);
        sw.WriteByte(BitsOfKey);
        sw.WriteByteArray(LookupTable);
    }

    /// <inheritdoc />
    public uint GetPackedSize()
    {
        uint size = sizeof(ulong) + //Seed
                    sizeof(uint) + //NumPartitions
                    sizeof(uint) + //RankTable length
                    (sizeof(uint) * (uint)RankTable.Length) + //RankTable
                    sizeof(byte) + //NumBitsOfKey
                    sizeof(uint) + //LookupTable length
                    (sizeof(byte) * (uint)LookupTable.Length); //LookupTable

        return size;
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BdzMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    public static BdzMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, HashFunc<TKey> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        ulong seed = sr.ReadUInt64();
        uint numPartitions = sr.ReadUInt32();
        uint[] rankTable = sr.ReadUInt32Array();
        byte numBitsOfKey = sr.ReadByte();
        byte[] lookupTable = sr.ReadByteArray();

        return new BdzMinimalState<TKey>(seed, numPartitions, numBitsOfKey, lookupTable, rankTable, hashFunc);
    }

    private static uint Rank(byte numBitsOfKey, uint[] rankTable, byte[] lookupTable, uint vertex)
    {
        uint index = vertex >> numBitsOfKey;
        uint baseRank = rankTable[index];
        uint begIdxV = index << numBitsOfKey;
        uint begIdxB = begIdxV >> 2;
        uint endIdxB = vertex >> 2;

        byte[] table = BdzShared.LookupTable.Value;

        while (begIdxB < endIdxB)
            baseRank += table[lookupTable[begIdxB++]];

        begIdxV = begIdxB << 2;

        while (begIdxV < vertex)
        {
            if (GetValue(lookupTable, begIdxV) != BdzShared.Unassigned)
                baseRank++;

            begIdxV++;
        }

        return baseRank;
    }
}