using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;
using static Genbox.FastMPH.Internals.BitArray;

namespace Genbox.FastMPH.BDZ;

/// <summary>Contains the state of a BDZ minimal perfect hash function</summary>
[PublicAPI]
public sealed class BdzMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode3<TKey> _hashCode;

    internal BdzMinimalState(uint partitions, byte[] lookupTable, uint seed, byte bitsOfKey, uint[] rankTable, HashCode3<TKey> hashCode)
    {
        _hashCode = hashCode;
        Partitions = partitions;
        LookupTable = lookupTable;
        Seed = seed;
        BitsOfKey = bitsOfKey;
        RankTable = rankTable;
    }

    /// <summary>The number of partitions</summary>
    public uint Partitions { get; }

    /// <summary>The lookup table</summary>
    public byte[] LookupTable { get; }

    /// <summary>The seed that was used for the hash function</summary>
    public uint Seed { get; }

    /// <summary>The number of bits per key. It determines the amount of information in the rank table. Larger values means more compact hash functions, but slower evaluation time.</summary>
    public byte BitsOfKey { get; }

    /// <summary>The rank table</summary>
    public uint[] RankTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        Span<uint> hashes = stackalloc uint[3];
        _hashCode(key, Seed, hashes);

        hashes[0] = hashes[0] % Partitions;
        hashes[1] = hashes[1] % Partitions + Partitions;
        hashes[2] = hashes[2] % Partitions + (Partitions << 1); // n + n * 2

        uint vertex = hashes[(GetValue(LookupTable, hashes[0]) + GetValue(LookupTable, hashes[1]) + GetValue(LookupTable, hashes[2])) % 3];
        return Rank(BitsOfKey, RankTable, LookupTable, vertex);
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(Seed);
        sw.WriteUInt32(Partitions);

        sw.WriteUInt32((uint)RankTable.Length);

        foreach (uint u in RankTable)
            sw.WriteUInt32(u);

        sw.WriteByte(BitsOfKey);

        sw.WriteUInt32((uint)LookupTable.Length);

        foreach (byte b in LookupTable)
            sw.WriteByte(b);
    }

    /// <inheritdoc />
    public uint GetPackedSize()
    {
        uint size = sizeof(uint) + //Seed
                    sizeof(uint) + //NumPartitions
                    sizeof(uint) + //LookupTable length
                    sizeof(byte) * (uint)LookupTable.Length + //LookupTable
                    sizeof(uint) + //RankTable length
                    sizeof(uint) * (uint)RankTable.Length + //RankTable
                    sizeof(byte); //NumBitsOfKey

        return size;
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BdzState{TKey}"/>
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function</param>
    public static BdzMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        SpanReader sr = new SpanReader(packed);

        uint seed = sr.ReadUInt32();
        uint numPartitions = sr.ReadUInt32();

        uint rankTableLength = sr.ReadUInt32();

        uint[] rankTable = new uint[rankTableLength];
        for (int i = 0; i < rankTableLength; i++)
            rankTable[i] = sr.ReadUInt32();

        byte numBitsOfKey = sr.ReadByte();

        uint lookupTableLength = sr.ReadUInt32();
        byte[] lookupTable = new byte[lookupTableLength];

        for (int i = 0; i < lookupTableLength; i++)
            lookupTable[i] = sr.ReadByte();

        return new BdzMinimalState<TKey>(numPartitions, lookupTable, seed, numBitsOfKey, rankTable, HashHelper.GetHashFunc3(comparer));
    }

    private static uint Rank(uint numBitsOfKey, uint[] rankTable, byte[] lookupTable, uint vertex)
    {
        uint index = vertex >> (int)numBitsOfKey;
        uint baseRank = rankTable[index];
        uint begIdxV = index << (int)numBitsOfKey;
        uint begIdxB = begIdxV >> 2;
        uint endIdxB = vertex >> 2;

        //Genbox: made the lookup table lazy loaded
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