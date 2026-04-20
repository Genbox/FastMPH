using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.CHD.Internal;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHD;

/// <summary>Contains the state of a CHD perfect hash function</summary>
[PublicAPI]
public sealed class ChdState<TKey> : IQueryState<TKey> where TKey : notnull
{
    private readonly CompressedSequence _cs;
    private readonly HashCode3<TKey> _hashCode;

    internal ChdState(CompressedSequence cs, uint numBuckets, uint numBins, uint numKeys, ulong seed, byte[] occupTable, HashFunc<TKey> hashFunc)
    {
        _cs = cs;
        _hashCode = HashHelper.GetHashFunc3(hashFunc);
        NumBuckets = numBuckets;
        NumBins = numBins;
        NumKeys = numKeys;
        Seed = seed;
        OccupTable = occupTable;
    }

    /// <summary>The seed used in the hash function</summary>
    public ulong Seed { get; }

    /// <summary>The number of buckets</summary>
    public uint NumBuckets { get; }

    /// <summary>The number of bins</summary>
    public uint NumBins { get; }

    internal uint NumKeys { get; }
    internal byte[] OccupTable { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        Span<uint> hashes = stackalloc uint[3];
        _hashCode(key, Seed, hashes);
        uint g = hashes[0] % NumBuckets;
        uint f = hashes[1] % NumBins;
        uint h = (hashes[2] % (NumBins - 1)) + 1;

        uint disp = _cs.Query(g);

        uint probe0Num = disp % NumBins;
        uint probe1Num = disp / NumBins;
        uint position = (uint)((f + ((ulong)h * probe0Num) + probe1Num) % NumBins);
        return position;
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(ulong) + //Seed
                                   sizeof(uint) + //NumBuckets
                                   sizeof(uint) + //NumBins
                                   sizeof(uint) + //NumKeys
                                   sizeof(uint) + //OccupTable length
                                   (sizeof(byte) * (uint)OccupTable.Length) + //OccupTable
                                   _cs.GetPackedSize();

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt64(Seed);
        sw.WriteUInt32(NumBuckets);
        sw.WriteUInt32(NumBins);
        sw.WriteUInt32(NumKeys);
        sw.WriteByteArray(OccupTable);
        _cs.Pack(sw);
    }

    /// <summary>
    /// Deserialize a serialized perfect hash function into a new instance of <see cref="ChdState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    public static ChdState<TKey> Unpack(ReadOnlySpan<byte> packed, HashFunc<TKey> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        ulong seed = sr.ReadUInt64();
        uint numBuckets = sr.ReadUInt32();
        uint numBins = sr.ReadUInt32();
        uint numKeys = sr.ReadUInt32();
        byte[] occupTable = sr.ReadByteArray();

        CompressedSequence cs = CompressedSequence.Unpack(sr);
        return new ChdState<TKey>(cs, numBuckets, numBins, numKeys, seed, occupTable, hashFunc);
    }
}