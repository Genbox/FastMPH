using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BBHash;

/// <summary>
/// BBHash is a scalable minimal perfect hash algorithm using a cascade of collision-free bitsets.
/// </summary>
[PublicAPI]
public sealed partial class BbHashBuilder<TKey> : IMinimalHashBuilder<TKey, BbHashMinimalState<TKey>, BbHashMinimalSettings> where TKey : notnull
{
    private const int RankSampleBits = 512;
    private const int RankSampleWords = RankSampleBits / 32;

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out BbHashMinimalState<TKey>? state, BbHashMinimalSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        BbHashBuildStatus status = CreateMinimalWithRemainder(keys, out BbHashBuildResult<TKey>? result, settings, comparer);

        if (status == BbHashBuildStatus.Success)
        {
            state = result.State;
            return true;
        }

        state = null;
        return false;
    }

    /// <summary>
    /// Create a minimal perfect hash function and return any remaining keys.
    /// </summary>
    /// <param name="keys">The keys you want to generate the hash function for.</param>
    /// <param name="settings">Settings for this hash function.</param>
    /// <param name="comparer">The equality comparer to use. If null, the object's own GetHashCode() will be called.</param>
    /// <param name="result">Contains the constructed state and any remaining keys. Null on failure.</param>
    /// <returns>Success if all keys are mapped; Partial if some remain; Failure for invalid inputs.</returns>
    public BbHashBuildStatus CreateMinimalWithRemainder(ReadOnlySpan<TKey> keys, out BbHashBuildResult<TKey>? result, BbHashMinimalSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new BbHashMinimalSettings();
        comparer ??= EqualityComparer<TKey>.Default;

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(comparer);

        LogCreating(keys.Length, settings.Gamma, settings.MaxLevels);

        uint seed0 = settings.Seed0;
        uint seed1 = settings.Seed1;

        int[] remaining = new int[keys.Length];

        for (int i = 0; i < keys.Length; i++)
            remaining[i] = i;

        int remainingCount = remaining.Length;
        uint offset = 0;

        List<uint> domains = new List<uint>();
        List<uint> offsets = new List<uint>();
        List<uint> bitsetStarts = new List<uint>();
        List<uint> rankStarts = new List<uint>();
        List<uint> bitsetWords = new List<uint>();
        List<uint> rankPrefixes = new List<uint>();

        if (!TryComputeInitialDomain(keys.Length, settings.Gamma, out double domainFloat))
        {
            result = null;
            return BbHashBuildStatus.Failure;
        }

        double collisionProbability = ComputeCollisionProbability(keys.Length, settings.Gamma);

        uint level = 0;

        while (remainingCount > 0 && level < settings.MaxLevels)
        {
            uint domain = RoundToBlock((uint)domainFloat);
            int wordCount = (int)((domain + 31) / 32);

            uint[] seen = new uint[wordCount];
            uint[] collisions = new uint[wordCount];

            LogLevelStart(level, remainingCount, domain);

            for (int i = 0; i < remainingCount; i++)
            {
                int keyIndex = remaining[i];
                uint pos = HashHelper.Reduce(BbHashHelper.GetLevelHash(keys[keyIndex], level, seed0, seed1, hashCode), domain);

                int word = (int)(pos >> 5);
                uint mask = 1u << (int)(pos & 31);

                if ((seen[word] & mask) != 0)
                    collisions[word] |= mask;
                else
                    seen[word] |= mask;
            }

            for (int i = 0; i < wordCount; i++)
                seen[i] &= ~collisions[i];

            int nextCount = 0;

            for (int i = 0; i < remainingCount; i++)
            {
                int keyIndex = remaining[i];
                uint pos = HashHelper.Reduce(BbHashHelper.GetLevelHash(keys[keyIndex], level, seed0, seed1, hashCode), domain);

                int word = (int)(pos >> 5);
                uint mask = 1u << (int)(pos & 31);

                if ((seen[word] & mask) == 0)
                    remaining[nextCount++] = keyIndex;
            }

            int placed = remainingCount - nextCount;

            domains.Add(domain);
            offsets.Add(offset);
            bitsetStarts.Add((uint)bitsetWords.Count);
            rankStarts.Add((uint)rankPrefixes.Count);

            bitsetWords.AddRange(seen);
            AppendRankPrefixes(seen, rankPrefixes);

            offset += (uint)placed;
            remainingCount = nextCount;
            LogLevelResult(level, placed, remainingCount);

            level++;
            domainFloat *= collisionProbability;
        }

        Dictionary<TKey, uint> remainder = new Dictionary<TKey, uint>(remainingCount, comparer);

        for (int i = 0; i < remainingCount; i++)
            remainder[keys[remaining[i]]] = offset + (uint)i;

        if (remainder.Count == 0)
            LogSuccess(domains.Count);
        else
            LogFailure();

        BbHashMinimalState<TKey> state = new BbHashMinimalState<TKey>(
            numKeys: (uint)keys.Length,
            seed0: seed0,
            seed1: seed1,
            domains: domains.ToArray(),
            offsets: offsets.ToArray(),
            bitsetStarts: bitsetStarts.ToArray(),
            rankStarts: rankStarts.ToArray(),
            bitsetWords: bitsetWords.ToArray(),
            rankPrefixes: rankPrefixes.ToArray(),
            hashCode);

        result = new BbHashBuildResult<TKey>(state, remainder);
        return remainder.Count == 0 ? BbHashBuildStatus.Success : BbHashBuildStatus.Partial;
    }

    private static bool TryComputeInitialDomain(int numKeys, double gamma, out double domainFloat)
    {
        domainFloat = Math.Ceiling(numKeys * gamma);

        if (double.IsNaN(domainFloat) || double.IsInfinity(domainFloat))
            return false;

        return domainFloat <= (uint.MaxValue - 31.0);
    }

    private static void AppendRankPrefixes(uint[] bitsetWords, List<uint> rankPrefixes)
    {
        uint sum = 0;

        for (int i = 0; i < bitsetWords.Length; i++)
        {
            if ((i % RankSampleWords) == 0)
                rankPrefixes.Add(sum);

            sum += (uint)BitOperations.PopCount(bitsetWords[i]);
        }
    }

    private static double ComputeCollisionProbability(int numKeys, double gamma)
    {
        if (numKeys <= 1)
            return 0.0;

        double gammaN = gamma * numKeys;
        return 1.0 - Math.Pow((gammaN - 1.0) / gammaN, numKeys - 1);
    }

    private static uint RoundToBlock(uint value)
    {
        uint rounded = (value + 31) & ~31u;
        return rounded == 0 ? 32u : rounded;
    }
}