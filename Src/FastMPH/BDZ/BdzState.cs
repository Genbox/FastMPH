using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BDZ;

/// <summary>Contains the state of a BDZ perfect hash function</summary>
[PublicAPI]
public sealed class BdzState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly HashCode3<TKey> _hashCode;

    internal BdzState(uint partitions, byte[] lookupTable, uint seed, HashCode3<TKey> hashCode)
    {
        _hashCode = hashCode;
        Partitions = partitions;
        LookupTable = lookupTable;
        Seed = seed;
    }

    /// <summary>The number of partitions</summary>
    public uint Partitions { get; }

    /// <summary>The lookup table</summary>
    public byte[] LookupTable { get; }

    /// <summary>The seed that was used for the hash function</summary>
    public uint Seed { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        Span<uint> hashes = stackalloc uint[3];

        _hashCode(key, Seed, hashes);
        hashes[0] = hashes[0] % Partitions;
        hashes[1] = hashes[1] % Partitions + Partitions;
        hashes[2] = hashes[2] % Partitions + (Partitions << 1); // n + n * 2

        byte byte0 = LookupTable[hashes[0] / 5];
        byte byte1 = LookupTable[hashes[1] / 5];
        byte byte2 = LookupTable[hashes[2] / 5];

        byte[][] lookup = BdzShared.SearchTable.Value;

        byte0 = lookup[hashes[0] % 5U][byte0];
        byte1 = lookup[hashes[1] % 5U][byte1];
        byte2 = lookup[hashes[2] % 5U][byte2];
        uint vertex = hashes[(byte0 + byte1 + byte2) % 3];
        return vertex;

        // int combined = (byte0 + byte1 + byte2) % 3;
        //
        // if (combined == 0)
        //     return h0;
        //
        // if (combined == 1)
        //     return h1;
        //
        // return h2;
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(Seed);
        sw.WriteUInt32(Partitions);
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
                    sizeof(byte) * (uint)LookupTable.Length; //LookupTable

        return size;
    }

    /// <summary>
    /// Deserialize a serialized perfect hash function into a new instance of <see cref="BdzState{TKey}"/>
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function</param>
    public static BdzState<TKey> Unpack(ReadOnlySpan<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        SpanReader sr = new SpanReader(packed);

        uint seed = sr.ReadUInt32();
        uint numPartitions = sr.ReadUInt32();

        uint lookupTableLength = sr.ReadUInt32();
        byte[] lookupTable = new byte[lookupTableLength];

        for (int i = 0; i < lookupTableLength; i++)
            lookupTable[i] = sr.ReadByte();

        return new BdzState<TKey>(numPartitions, lookupTable, seed, HashHelper.GetHashFunc3(comparer));
    }
}