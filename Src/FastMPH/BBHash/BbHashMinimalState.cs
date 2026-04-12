using System.Numerics;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BBHash;

/// <summary>Contains the state of a BBHash minimal perfect hash function.</summary>
[PublicAPI]
public sealed class BbHashMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private const int RankSampleBits = 512;
    private const int RankSampleWords = RankSampleBits / 32;

    private readonly HashCode<TKey> _hashCode;

    internal BbHashMinimalState(uint numKeys, uint seed0, uint seed1, uint[] domains, uint[] offsets, uint[] bitsetStarts, uint[] rankStarts, uint[] bitsetWords, uint[] rankPrefixes, HashCode<TKey> hashCode)
    {
        NumKeys = numKeys;
        Seed0 = seed0;
        Seed1 = seed1;
        Domains = domains;
        Offsets = offsets;
        BitsetStarts = bitsetStarts;
        RankStarts = rankStarts;
        BitsetWords = bitsetWords;
        RankPrefixes = rankPrefixes;
        _hashCode = hashCode;
    }

    /// <summary>The number of keys in the original set.</summary>
    public uint NumKeys { get; }

    /// <summary>The first hash seed.</summary>
    public uint Seed0 { get; }

    /// <summary>The second hash seed.</summary>
    public uint Seed1 { get; }

    /// <summary>Domain size for each level.</summary>
    public uint[] Domains { get; }

    /// <summary>Base offset for each level.</summary>
    public uint[] Offsets { get; }

    /// <summary>Start index for each level in <see cref="BitsetWords"/>.</summary>
    public uint[] BitsetStarts { get; }

    /// <summary>Start index for each level in <see cref="RankPrefixes"/>.</summary>
    public uint[] RankStarts { get; }

    /// <summary>Flattened bitset words (32-bit).</summary>
    public uint[] BitsetWords { get; }

    /// <summary>Flattened rank prefix arrays (32-bit).</summary>
    public uint[] RankPrefixes { get; }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        for (int level = 0; level < Domains.Length; level++)
        {
            uint domain = Domains[level];
            uint pos = HashHelper.Reduce(BbHashHelper.GetLevelHash(key, (uint)level, Seed0, Seed1, _hashCode), domain);

            int word = (int)(pos >> 5);
            int bit = (int)(pos & 31);

            int bitsetStart = (int)BitsetStarts[level];
            int bitWordIndex = bitsetStart + word;
            uint wordValue = BitsetWords[bitWordIndex];

            uint mask = 1u << bit;

            if ((wordValue & mask) == 0)
                continue;

            int rankStart = (int)RankStarts[level];
            int block = word / RankSampleWords;
            uint prefix = RankPrefixes[rankStart + block];

            uint withinBlock = 0;
            int blockStart = block * RankSampleWords;

            for (int w = blockStart; w < word; w++)
                withinBlock += (uint)BitOperations.PopCount(BitsetWords[bitsetStart + w]);

            uint lowerMask = bit == 0 ? 0u : ((1u << bit) - 1);
            uint withinWord = (uint)BitOperations.PopCount(wordValue & lowerMask);

            return Offsets[level] + prefix + withinBlock + withinWord;
        }

        return 0;
    }

    /// <inheritdoc />
    public uint GetPackedSize()
    {
        return sizeof(uint) + // NumKeys
               sizeof(uint) + // Seed0
               sizeof(uint) + // Seed1
               sizeof(uint) + // Domains length
               (sizeof(uint) * (uint)Domains.Length) +
               sizeof(uint) + // Offsets length
               (sizeof(uint) * (uint)Offsets.Length) +
               sizeof(uint) + // BitsetStarts length
               (sizeof(uint) * (uint)BitsetStarts.Length) +
               sizeof(uint) + // RankStarts length
               (sizeof(uint) * (uint)RankStarts.Length) +
               sizeof(uint) + // BitsetWords length
               (sizeof(uint) * (uint)BitsetWords.Length) +
               sizeof(uint) + // RankPrefixes length
               (sizeof(uint) * (uint)RankPrefixes.Length);
    }

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(NumKeys);
        sw.WriteUInt32(Seed0);
        sw.WriteUInt32(Seed1);

        WriteUInt32Array(ref sw, Domains);
        WriteUInt32Array(ref sw, Offsets);
        WriteUInt32Array(ref sw, BitsetStarts);
        WriteUInt32Array(ref sw, RankStarts);
        WriteUInt32Array(ref sw, BitsetWords);
        WriteUInt32Array(ref sw, RankPrefixes);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BbHashMinimalState{TKey}" />.
    /// </summary>
    /// <param name="packed">The serialized hash function.</param>
    /// <param name="hashFunc">The hash function that was used when creating the hash function.</param>
    public static BbHashMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, Func<TKey, uint> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        uint numKeys = sr.ReadUInt32();
        uint seed0 = sr.ReadUInt32();
        uint seed1 = sr.ReadUInt32();

        uint[] domains = ReadUInt32Array(ref sr);
        uint[] offsets = ReadUInt32Array(ref sr);
        uint[] bitsetStarts = ReadUInt32Array(ref sr);
        uint[] rankStarts = ReadUInt32Array(ref sr);
        uint[] bitsetWords = ReadUInt32Array(ref sr);
        uint[] rankPrefixes = ReadUInt32Array(ref sr);

        return new BbHashMinimalState<TKey>(numKeys, seed0, seed1, domains, offsets, bitsetStarts, rankStarts, bitsetWords, rankPrefixes, HashHelper.GetHashFunc(hashFunc));
    }

    private static void WriteUInt32Array(ref SpanWriter sw, uint[] values)
    {
        sw.WriteUInt32((uint)values.Length);

        foreach (uint value in values)
            sw.WriteUInt32(value);
    }

    private static uint[] ReadUInt32Array(ref SpanReader sr)
    {
        uint length = sr.ReadUInt32();
        uint[] values = new uint[length];

        for (int i = 0; i < length; i++)
            values[i] = sr.ReadUInt32();

        return values;
    }
}