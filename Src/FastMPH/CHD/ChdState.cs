using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.CHD.Internal;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHD;

/// <summary>Contains the state of a CHD perfect hash function</summary>
[PublicAPI]
public sealed class ChdState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly CompressedSequence _cs;
    private readonly HashCode3<TKey> _hashCode;
    internal readonly uint NumKeys;
    internal readonly byte[] OccupTable;

    internal ChdState(CompressedSequence cs, uint numBuckets, uint numBins, uint numKeys, uint seed, byte[] occupTable, HashCode3<TKey> hashCode)
    {
        _cs = cs;
        NumBuckets = numBuckets;
        NumBins = numBins;
        NumKeys = numKeys;
        Seed = seed;
        OccupTable = occupTable;
        _hashCode = hashCode;
    }

    /// <summary>The seed used in the hash function</summary>
    public uint Seed { get; }

    /// <summary>The number of buckets</summary>
    public uint NumBuckets { get; }

    /// <summary>The number of bins</summary>
    public uint NumBins { get; }

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
    public uint GetPackedSize()
    {
        uint size = sizeof(uint) + //Seed
                    sizeof(uint) + //NumBuckets
                    sizeof(uint) + //NumBins
                    sizeof(uint) + //NumKeys
                    sizeof(uint) + //OccupTable length
                    (sizeof(byte) * (uint)OccupTable.Length) + //OccupTable
                    sizeof(uint) + //OccupTable length
                    _cs.GetPackedSize();

        return size;
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(Seed);
        sw.WriteUInt32(NumBuckets);
        sw.WriteUInt32(NumBins);
        sw.WriteUInt32(NumKeys);
        sw.WriteUInt32((uint)OccupTable.Length);

        foreach (byte b in OccupTable)
            sw.WriteByte(b);

        _cs.Pack(sw);
    }

    /// <summary>
    /// Deserialize a serialized perfect hash function into a new instance of <see cref="ChdState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    /// <param name="comparer">The equality comparer that was used when packing the hash function</param>
    public static ChdState<TKey> Unpack(ReadOnlySpan<byte> packed, IEqualityComparer<TKey>? comparer = null)
    {
        comparer ??= EqualityComparer<TKey>.Default;

        SpanReader sr = new SpanReader(packed);
        uint seed = sr.ReadUInt32();
        uint numBuckets = sr.ReadUInt32();
        uint numBins = sr.ReadUInt32();
        uint numKeys = sr.ReadUInt32();
        uint length = sr.ReadUInt32();

        byte[] occupTable = new byte[length];

        for (int i = 0; i < length; i++)
            occupTable[i] = sr.ReadByte();

        CompressedSequence cs = CompressedSequence.Unpack(sr);
        return new ChdState<TKey>(cs, numBuckets, numBins, numKeys, seed, occupTable, HashHelper.GetHashFunc3(comparer));
    }
}