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
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, [NotNullWhen(true)]out BbHashMinimalState<TKey>? state, BbHashMinimalSettings? settings = null)
    {
        BbHashBuildStatus status = CreateMinimalWithRemainder(keys, hashFunc, seed, out BbHashBuildResult<TKey>? result, settings);

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
    /// <param name="hashFunc">The hash function for keys.</param>
    /// <param name="settings">Settings for this hash function.</param>
    /// <param name="result">Contains the constructed state and any remaining keys. Null on failure.</param>
    /// <returns>Success if all keys are mapped; Partial if some remain; Failure for invalid inputs.</returns>
    public BbHashBuildStatus CreateMinimalWithRemainder(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, out BbHashBuildResult<TKey>? result, BbHashMinimalSettings? settings = null)
    {
        settings ??= new BbHashMinimalSettings();

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(hashFunc);

        BbHashBuildState buildState = new BbHashBuildState();

        return CreateMinimalWithRemainderCore(keys, hashCode, settings, seed, buildState, out result);
    }

    private BbHashBuildStatus CreateMinimalWithRemainderCore(ReadOnlySpan<TKey> keys, HashCode<TKey> hashCode, BbHashMinimalSettings settings, ulong seed, BbHashBuildState buildState, out BbHashBuildResult<TKey>? result)
    {
        LogCreating(keys.Length, settings.Gamma, settings.MaxLevels);

        buildState.EnsureForRemaining(keys.Length);
        int[] remaining = buildState.Remaining;

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

            buildState.EnsureForWords(wordCount);
            uint[] seen = buildState.Seen;
            uint[] collisions = buildState.Collisions;
            Array.Clear(seen, 0, wordCount);
            Array.Clear(collisions, 0, wordCount);

            LogLevelStart(level, remainingCount, domain);

            for (int i = 0; i < remainingCount; i++)
            {
                int keyIndex = remaining[i];
                uint pos = HashHelper.Reduce(BbHashHelper.GetLevelHash(keys[keyIndex], level, seed, hashCode), domain);

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
                uint pos = HashHelper.Reduce(BbHashHelper.GetLevelHash(keys[keyIndex], level, seed, hashCode), domain);

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

            for (int i = 0; i < wordCount; i++)
                bitsetWords.Add(seen[i]);

            AppendRankPrefixes(seen, wordCount, rankPrefixes);

            offset += (uint)placed;
            remainingCount = nextCount;
            LogLevelResult(level, placed, remainingCount);

            level++;
            domainFloat *= collisionProbability;
        }

        Dictionary<TKey, uint> remainder = new Dictionary<TKey, uint>(remainingCount);

        for (int i = 0; i < remainingCount; i++)
            remainder[keys[remaining[i]]] = offset + (uint)i;

        if (remainder.Count == 0)
            LogSuccess(domains.Count);
        else
            LogFailure();

        BbHashMinimalState<TKey> state = new BbHashMinimalState<TKey>(
            numKeys: (uint)keys.Length,
            seed: seed,
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

    private sealed class BbHashBuildState
    {
        public int[] Remaining = [];
        public uint[] Seen = [];
        public uint[] Collisions = [];

        public void EnsureForRemaining(int count)
        {
            ArrayEnsure.EnsureCapacity(ref Remaining, count);
        }

        public void EnsureForWords(int count)
        {
            ArrayEnsure.EnsureCapacity(ref Seen, count);
            ArrayEnsure.EnsureCapacity(ref Collisions, count);
        }
    }

    private static bool TryComputeInitialDomain(int numKeys, double gamma, out double domainFloat)
    {
        domainFloat = Math.Ceiling(numKeys * gamma);

        if (double.IsNaN(domainFloat) || double.IsInfinity(domainFloat))
            return false;

        return domainFloat <= (uint.MaxValue - 31.0);
    }

    private static void AppendRankPrefixes(uint[] bitsetWords, int wordCount, List<uint> rankPrefixes)
    {
        uint sum = 0;

        for (int i = 0; i < wordCount; i++)
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