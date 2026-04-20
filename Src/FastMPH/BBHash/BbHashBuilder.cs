using System.Buffers;
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
    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out BbHashMinimalState<TKey>? state, BbHashMinimalSettings? settings = null)
    {
        settings ??= new BbHashMinimalSettings();

        if (!TryCreateMinimalState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateMinimalCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateMinimalState(int numKeys, BbHashMinimalSettings settings, [NotNullWhen(true)]out IBuildState? state)
    {
        if (!BuildState.TryCreate(numKeys, settings, out BuildState? typed))
        {
            state = null;
            return false;
        }

        state = typed;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, BbHashMinimalSettings settings, [NotNullWhen(true)]out BbHashMinimalState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        build.Reset();

        LogCreating(keys.Length, settings.Gamma, settings.MaxLevels);

        int remainingCount = keys.Length;
        uint offset = 0;
        uint level = 0;
        double domainFloat = build.InitialDomainFloat;

        int initialWordCount = (int)((RoundToBlock((uint)domainFloat) + 31) / 32);
        uint[] seen = ArrayPool<uint>.Shared.Rent(initialWordCount);
        uint[] collisions = ArrayPool<uint>.Shared.Rent(initialWordCount);

        try
        {
            Array.Clear(seen, 0, initialWordCount);
            Array.Clear(collisions, 0, initialWordCount);

            while (remainingCount > 0 && level < settings.MaxLevels)
            {
                uint domain = RoundToBlock((uint)domainFloat);
                int wordCount = (int)((domain + 31) / 32);

                if (wordCount > seen.Length)
                {
                    ArrayPool<uint>.Shared.Return(seen);
                    seen = ArrayPool<uint>.Shared.Rent(wordCount);
                }

                if (wordCount > collisions.Length)
                {
                    ArrayPool<uint>.Shared.Return(collisions);
                    collisions = ArrayPool<uint>.Shared.Rent(wordCount);
                }

                Array.Clear(seen, 0, wordCount);
                Array.Clear(collisions, 0, wordCount);

                LogLevelStart(level, remainingCount, domain);

                for (int i = 0; i < remainingCount; i++)
                {
                    uint pos = HashHelper.Reduce(BbHashShared.GetLevelHash(keys[build.Remaining[i]], level, seed, hashFunc), domain);

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
                    int keyIndex = build.Remaining[i];
                    uint pos = HashHelper.Reduce(BbHashShared.GetLevelHash(keys[keyIndex], level, seed, hashFunc), domain);

                    int word = (int)(pos >> 5);
                    uint mask = 1u << (int)(pos & 31);

                    if ((seen[word] & mask) == 0)
                        build.Remaining[nextCount++] = keyIndex;
                }

                int placed = remainingCount - nextCount;

                build.Domains.Add(domain);
                build.Offsets.Add(offset);
                build.BitsetStarts.Add((uint)build.BitsetWords.Count);
                build.RankStarts.Add((uint)build.RankPrefixes.Count);

                for (int i = 0; i < wordCount; i++)
                    build.BitsetWords.Add(seen[i]);

                AppendRankPrefixes(seen, wordCount, build.RankPrefixes);

                offset += (uint)placed;
                remainingCount = nextCount;
                LogLevelResult(level, placed, remainingCount);

                level++;
                domainFloat *= build.CollisionProbability;
            }

            if (remainingCount != 0)
            {
                LogFailure();
                queryState = null;
                return false;
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(seen);
            ArrayPool<uint>.Shared.Return(collisions);
        }

        LogSuccess(build.Domains.Count);

        queryState = new BbHashMinimalState<TKey>(
            seed,
            build.Domains.ToArray(),
            build.Offsets.ToArray(),
            build.BitsetStarts.ToArray(),
            build.RankStarts.ToArray(),
            build.BitsetWords.ToArray(),
            build.RankPrefixes.ToArray(),
            hashFunc);

        return true;
    }

    private static void AppendRankPrefixes(uint[] bitsetWords, int wordCount, List<uint> rankPrefixes)
    {
        uint sum = 0;

        for (int i = 0; i < wordCount; i++)
        {
            if (i % BbHashShared.RankSampleWords == 0)
                rankPrefixes.Add(sum);

            sum += (uint)BitOperations.PopCount(bitsetWords[i]);
        }
    }

    private static uint RoundToBlock(uint value)
    {
        uint rounded = (value + 31) & ~31u;
        return rounded == 0 ? 32u : rounded;
    }

    private sealed class BuildState : IBuildState
    {
        public double InitialDomainFloat;
        public double CollisionProbability;
        public int[] Remaining = [];
        public List<uint> Domains = [];
        public List<uint> Offsets = [];
        public List<uint> BitsetStarts = [];
        public List<uint> RankStarts = [];
        public List<uint> BitsetWords = [];
        public List<uint> RankPrefixes = [];

        public static bool TryCreate(int numKeys, BbHashMinimalSettings settings, [NotNullWhen(true)]out BuildState? state)
        {
            double domainFloat = Math.Ceiling(numKeys * settings.Gamma);

            if (double.IsNaN(domainFloat) || double.IsInfinity(domainFloat) || domainFloat > uint.MaxValue - 31.0)
            {
                state = null;
                return false;
            }

            double collisionProbability = ComputeCollisionProbability(numKeys, settings.Gamma);

            state = new BuildState
            {
                InitialDomainFloat = domainFloat,
                CollisionProbability = collisionProbability,
                Remaining = GC.AllocateUninitializedArray<int>(numKeys),
                Domains = new List<uint>((numKeys / 8) + 1),
                Offsets = new List<uint>((numKeys / 8) + 1),
                BitsetStarts = new List<uint>((numKeys / 8) + 1),
                RankStarts = new List<uint>((numKeys / 8) + 1),
                BitsetWords = new List<uint>(numKeys + 1),
                RankPrefixes = new List<uint>(numKeys + 1)
            };

            state.Reset();
            return true;
        }

        private static double ComputeCollisionProbability(int numKeys, double gamma)
        {
            if (numKeys <= 1)
                return 0.0;

            double gammaN = gamma * numKeys;
            return 1.0 - Math.Pow((gammaN - 1.0) / gammaN, numKeys - 1);
        }

        public void Reset()
        {
            for (int i = 0; i < Remaining.Length; i++)
                Remaining[i] = i;

            Domains.Clear();
            Offsets.Clear();
            BitsetStarts.Clear();
            RankStarts.Clear();
            BitsetWords.Clear();
            RankPrefixes.Clear();
        }
    }
}