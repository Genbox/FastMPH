using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.CHD.Internal;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHD;

/// <summary>Contains the state of a CHD perfect hash function</summary>
[PublicAPI]
public sealed class ChdState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly CompressedSequence _cs;
    internal readonly byte[] OccupTable;
    private readonly Func<TKey, uint, uint[]> _hashCode;
    internal readonly uint NumKeys;

    internal ChdState(CompressedSequence cs, uint buckets, uint bins, uint numKeys, uint seed, byte[] occupTable, Func<TKey, uint, uint[]> hashCode)
    {
        _cs = cs;
        Buckets = buckets;
        Bins = bins;
        NumKeys = numKeys;
        Seed = seed;
        OccupTable = occupTable;
        _hashCode = hashCode;
    }

    /// <summary>The seed used in the hash function</summary>
    public uint Seed { get; }

    /// <summary>The number of buckets</summary>
    public uint Buckets { get; }

    /// <summary>The number of bins</summary>
    public uint Bins { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint[] hashes = _hashCode(key, Seed);
        uint g = hashes[0] % Buckets;
        uint f = hashes[1] % Bins;
        uint h = hashes[2] % (Bins - 1) + 1;

        uint disp = _cs.Query(g);

        uint probe0Num = disp % Bins;
        uint probe1Num = disp / Bins;
        uint position = (uint)((f + (ulong)h * probe0Num + probe1Num) % Bins);
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
                    sizeof(byte) * (uint)OccupTable.Length + //OccupTable
                    sizeof(uint); //OccupTable length

        //TODO: sizeof(byte) * (uint)_cs; //LookupTable

        return size;
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(Seed);
        sw.WriteUInt32(Buckets);
        sw.WriteUInt32(Bins);
        sw.WriteUInt32(NumKeys);
        sw.WriteUInt32((uint)OccupTable.Length);

        foreach (byte b in OccupTable)
            sw.WriteByte(b);

        //TODO: compressed seq
    }

    /// <summary>
    /// Deserialize a serialized perfect hash function into a new instance of <see cref="ChdState"/>
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    public static ChdState<TKey> Unpack(ReadOnlySpan<byte> packed)
    {
        SpanReader sr = new SpanReader(packed);
        uint seed = sr.ReadUInt32();
        uint numBuckets = sr.ReadUInt32();
        uint numBins = sr.ReadUInt32();
        uint numKeys = sr.ReadUInt32();
        uint length = sr.ReadUInt32();

        byte[] occupTable = new byte[length];

        for (int i = 0; i < length; i++)
            occupTable[i] = sr.ReadByte();

        //TODO: compressed seq

        return new ChdState<TKey>(null!, numBuckets, numBins, numKeys, seed, occupTable, null!);
    }
}