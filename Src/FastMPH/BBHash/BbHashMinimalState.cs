using System.Numerics;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BBHash;

/// <summary>Contains the state of a BBHash minimal perfect hash function.</summary>
[PublicAPI]
public sealed class BbHashMinimalState<TKey> : IQueryState<TKey> where TKey : notnull
{
    private readonly HashFunc<TKey> _hashFunc;

    internal BbHashMinimalState(ulong seed, uint[] domains, uint[] offsets, uint[] bitsetStarts, uint[] rankStarts, uint[] bitsetWords, uint[] rankPrefixes, HashFunc<TKey> hashFunc)
    {
        Seed = seed;
        Domains = domains;
        Offsets = offsets;
        BitsetStarts = bitsetStarts;
        RankStarts = rankStarts;
        BitsetWords = bitsetWords;
        RankPrefixes = rankPrefixes;
        _hashFunc = hashFunc;
    }

    /// <summary>The hash seed.</summary>
    public ulong Seed { get; }

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
            uint pos = HashHelper.Reduce(BbHashShared.GetLevelHash(key, (uint)level, Seed, _hashFunc), domain);

            int word = (int)(pos >> 5);
            int bit = (int)(pos & 31);

            int bitsetStart = (int)BitsetStarts[level];
            int bitWordIndex = bitsetStart + word;
            uint wordValue = BitsetWords[bitWordIndex];

            uint mask = 1u << bit;

            if ((wordValue & mask) == 0)
                continue;

            int rankStart = (int)RankStarts[level];
            int block = word / BbHashShared.RankSampleWords;
            uint prefix = RankPrefixes[rankStart + block];

            uint withinBlock = 0;
            int blockStart = block * BbHashShared.RankSampleWords;

            for (int w = blockStart; w < word; w++)
                withinBlock += (uint)BitOperations.PopCount(BitsetWords[bitsetStart + w]);

            uint lowerMask = bit == 0 ? 0u : ((1u << bit) - 1);
            uint withinWord = (uint)BitOperations.PopCount(wordValue & lowerMask);

            return Offsets[level] + prefix + withinBlock + withinWord;
        }

        return 0;
    }

    /// <inheritdoc />
    public uint GetPackedSize() => sizeof(ulong) + // Seed
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

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt64(Seed);
        sw.WriteUInt32Array(Domains);
        sw.WriteUInt32Array(Offsets);
        sw.WriteUInt32Array(BitsetStarts);
        sw.WriteUInt32Array(RankStarts);
        sw.WriteUInt32Array(BitsetWords);
        sw.WriteUInt32Array(RankPrefixes);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="BbHashMinimalState{TKey}" />.
    /// </summary>
    /// <param name="packed">The serialized hash function.</param>
    public static BbHashMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed, HashFunc<TKey> hashFunc)
    {
        SpanReader sr = new SpanReader(packed);
        ulong seed = sr.ReadUInt64();

        uint[] domains = sr.ReadUInt32Array();
        uint[] offsets = sr.ReadUInt32Array();
        uint[] bitsetStarts = sr.ReadUInt32Array();
        uint[] rankStarts = sr.ReadUInt32Array();
        uint[] bitsetWords = sr.ReadUInt32Array();
        uint[] rankPrefixes = sr.ReadUInt32Array();

        return new BbHashMinimalState<TKey>(seed, domains, offsets, bitsetStarts, rankStarts, bitsetWords, rankPrefixes, hashFunc);
    }
}