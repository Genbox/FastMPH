using Genbox.FastMPH.BBHash;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class BbHashTests
{
    private static readonly HashFunc<int> _intHash = static (value, seed) => unchecked((ulong)value * seed);
    private static readonly HashFunc<string> _ordinalIgnoreCaseHash = static (value, seed) => unchecked((ulong)StringComparer.OrdinalIgnoreCase.GetHashCode(value) * seed);

    [Fact]
    public void HashCollisionReturnsFailure()
    {
        ulong[] values = [0UL, 1UL];
        BbHashBuilder<ulong> builder = new BbHashBuilder<ulong>(NullLogger<BbHashBuilder<ulong>>.Instance);

        // Force a collision by returning the same hash for both values
        HashFunc<ulong> collidingHash = static (_, _) => 42UL;

        Assert.False(builder.TryCreateMinimalWithRetry(values, collidingHash, out _));
    }

    [Fact]
    public void MapsKeysIntoMinimalRange()
    {
        int[] values = Enumerable.Range(0, 2000).Select(i => (i * 37) + 11).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);

        Assert.True(builder.TryCreateMinimalWithRetry(values, _intHash, out BbHashMinimalState<int>? state));
        Assert.NotNull(state);

        HashSet<uint> seen = new HashSet<uint>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            uint index = state.Search(values[i]);
            Assert.True(index < (uint)values.Length);
            Assert.True(seen.Add(index), $"Duplicate hash index {index} for key {values[i]}");
        }

        Assert.Equal(values.Length, seen.Count);
    }

    [Fact]
    public void RoundtripPreservesSearchResults()
    {
        int[] values = Enumerable.Range(0, 3000).Select(i => (i * 53) + 7).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);

        Assert.True(builder.TryCreateMinimalWithRetry(values, _intHash, out BbHashMinimalState<int>? state));
        Assert.NotNull(state);

        byte[] packed = new byte[state.GetPackedSize()];
        state.Pack(packed);

        BbHashMinimalState<int> unpacked = BbHashMinimalState<int>.Unpack(packed, _intHash);

        for (int i = 0; i < values.Length; i++)
            Assert.Equal(state.Search(values[i]), unpacked.Search(values[i]));
    }

    [Fact]
    public void FirstLevelPlacesManyKeys()
    {
        int[] values = Enumerable.Range(0, 1000).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);

        Assert.True(builder.TryCreateMinimalWithRetry(values, _intHash, out BbHashMinimalState<int>? state));
        Assert.NotNull(state);
        Assert.NotEmpty(state.Domains);

        uint firstLevelPlaced = state.Offsets.Length > 1 ? state.Offsets[1] - state.Offsets[0] : (uint)values.Length;
        Assert.True(firstLevelPlaced > (uint)(values.Length / 10));
    }

    [Fact]
    public void ReturnsFailureWhenMaxLevelsIsZero()
    {
        int[] values = Enumerable.Range(100, 64).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);
        BbHashMinimalSettings settings = new BbHashMinimalSettings { MaxLevels = 0 };

        Assert.False(builder.TryCreateMinimalWithRetry(values, _intHash, out _, settings));
    }

    [Fact]
    public void SupportsCustomHashForLookup()
    {
        string[] values = ["alpha", "beta", "gamma", "delta"];
        BbHashBuilder<string> builder = new BbHashBuilder<string>(NullLogger<BbHashBuilder<string>>.Instance);

        Assert.True(builder.TryCreateMinimalWithRetry(values, _ordinalIgnoreCaseHash, out BbHashMinimalState<string>? state));
        Assert.NotNull(state);

        Assert.Equal(state.Search("alpha"), state.Search("ALPHA"));
        Assert.Equal(state.Search("beta"), state.Search("BeTa"));
        Assert.Equal(state.Search("gamma"), state.Search("GAMMA"));
        Assert.Equal(state.Search("delta"), state.Search("DELTA"));
    }

    [Fact]
    public void ReturnsFailureWhenInitialDomainExceedsUIntRange()
    {
        int[] values = [1, 2];
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);
        BbHashMinimalSettings settings = new BbHashMinimalSettings { Gamma = uint.MaxValue };

        Assert.False(builder.TryCreateMinimalWithRetry(values, _intHash, out _, settings));
    }
}