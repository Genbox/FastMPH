using Genbox.FastMPH.BBHash;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class BbHashTests
{
    private static readonly Func<int, uint> _intHash = value => unchecked((uint)value.GetHashCode());
    private static readonly Func<ulong, uint> _ulongHash = value => unchecked((uint)value.GetHashCode());
    private static readonly Func<string, uint> _ordinalIgnoreCaseHash = value => unchecked((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(value));

    [Fact]
    public void ULongHashCodeCollisionReturnsPartial()
    {
        ulong[] values = [0UL, 0x0000000100000001UL];
        BbHashBuilder<ulong> builder = new BbHashBuilder<ulong>(NullLogger<BbHashBuilder<ulong>>.Instance);

        Assert.Equal(values[0].GetHashCode(), values[1].GetHashCode());

        BbHashBuildStatus status = builder.CreateMinimalWithRemainder(values, _ulongHash, out BbHashBuildResult<ulong>? result);

        Assert.Equal(BbHashBuildStatus.Partial, status);
        Assert.NotNull(result);
        Assert.False(builder.TryCreateMinimal(values, _ulongHash, out _));

        Assert.Equal(values.Length, result.Remainder.Count);
        Assert.Contains(values[0], result.Remainder.Keys);
        Assert.Contains(values[1], result.Remainder.Keys);
        Assert.Contains(0u, result.Remainder.Values);
        Assert.Contains(1u, result.Remainder.Values);
    }

    [Fact]
    public void MapsKeysIntoMinimalRange()
    {
        int[] values = Enumerable.Range(0, 2000).Select(i => (i * 37) + 11).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);

        Assert.True(builder.TryCreateMinimal(values, _intHash, out BbHashMinimalState<int>? state));
        Assert.NotNull(state);

        for (int i = 0; i < values.Length; i++)
        {
            uint index = state.Search(values[i]);
            Assert.True(index < (uint)values.Length);
        }
    }

    [Fact]
    public void FirstLevelPlacesManyKeys()
    {
        int[] values = Enumerable.Range(0, 1000).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);

        Assert.True(builder.TryCreateMinimal(values, _intHash, out BbHashMinimalState<int>? state));
        Assert.NotNull(state);
        Assert.NotEmpty(state.Domains);

        uint firstLevelPlaced = state.Offsets.Length > 1 ? state.Offsets[1] - state.Offsets[0] : state.NumKeys;
        Assert.True(firstLevelPlaced > (uint)(values.Length / 10));
    }

    [Fact]
    public void ReturnsPartialWithRemainderWhenMaxLevelsIsZero()
    {
        int[] values = Enumerable.Range(100, 64).ToArray();
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);
        BbHashMinimalSettings settings = new BbHashMinimalSettings { MaxLevels = 0 };

        BbHashBuildStatus status = builder.CreateMinimalWithRemainder(values, _intHash, out BbHashBuildResult<int>? result, settings);

        Assert.Equal(BbHashBuildStatus.Partial, status);
        Assert.NotNull(result);
        Assert.False(builder.TryCreateMinimal(values, _intHash, out _, settings));

        Assert.Empty(result.State.Domains);
        Assert.Equal((uint)values.Length, result.State.NumKeys);
        Assert.Equal(values.Length, result.Remainder.Count);

        HashSet<uint> remainderIndexes = result.Remainder.Values.ToHashSet();
        Assert.Equal(values.Length, remainderIndexes.Count);

        for (uint i = 0; i < (uint)values.Length; i++)
            Assert.Contains(i, remainderIndexes);
    }

    [Fact]
    public void SupportsCustomHashForLookup()
    {
        string[] values = ["alpha", "beta", "gamma", "delta"];
        BbHashBuilder<string> builder = new BbHashBuilder<string>(NullLogger<BbHashBuilder<string>>.Instance);

        Assert.True(builder.TryCreateMinimal(values, _ordinalIgnoreCaseHash, out BbHashMinimalState<string>? state));
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

        BbHashBuildStatus status = builder.CreateMinimalWithRemainder(values, _intHash, out BbHashBuildResult<int>? result, settings);

        Assert.Equal(BbHashBuildStatus.Failure, status);
        Assert.Null(result);
        Assert.False(builder.TryCreateMinimal(values, _intHash, out _, settings));
    }
}