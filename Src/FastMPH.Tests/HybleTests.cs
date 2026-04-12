using Genbox.FastMPH.Hyble;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class HybleTests
{
    private static readonly Func<int, ulong> _intHash = value => unchecked((ulong)value);
    private static readonly Func<string, ulong> _ordinalIgnoreCaseHash = value => unchecked((ulong)StringComparer.OrdinalIgnoreCase.GetHashCode(value));

    [Fact]
    public void BuildsPerfectHashForIntegers()
    {
        int[] values = Enumerable.Range(0, 5000).Select(i => (i * 53) + 19).ToArray();
        HybleBuilder<int> builder = new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance);

        Assert.True(builder.TryCreateWithRetry(values, _intHash, out HybleState<int>? state));
        Assert.NotNull(state);

        HashSet<uint> seen = new HashSet<uint>();

        for (int i = 0; i < values.Length; i++)
            Assert.True(seen.Add(state.Search(values[i])));
    }

    [Fact]
    public void RoundtripPreservesSearchResultsWhenSeedSerialized()
    {
        int[] values = Enumerable.Range(0, 4000).Select(i => (i * 97) + 31).ToArray();
        HybleBuilder<int> builder = new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance);

        Assert.True(builder.TryCreateWithRetry(values, _intHash, out HybleState<int>? state));
        Assert.NotNull(state);

        byte[] packed = new byte[state.GetPackedSize()];
        state.Pack(packed);

        HybleState<int> unpacked = HybleState<int>.Unpack(packed, _intHash);

        Assert.Equal(state.ApproxRange, unpacked.ApproxRange);
        Assert.Equal(state.Displacements, unpacked.Displacements);
        Assert.Equal(state.Seed, unpacked.Seed);

        foreach (int value in values)
            Assert.Equal(state.Search(value), unpacked.Search(value));
    }

    [Fact]
    public void SupportsCustomHashForLookup()
    {
        string[] values = ["one", "two", "three", "four"];
        HybleBuilder<string> builder = new HybleBuilder<string>(NullLogger<HybleBuilder<string>>.Instance);

        Assert.True(builder.TryCreateWithRetry(values, _ordinalIgnoreCaseHash, out HybleState<string>? state));
        Assert.NotNull(state);

        Assert.Equal(state.Search("one"), state.Search("ONE"));
        Assert.Equal(state.Search("two"), state.Search("TWO"));
        Assert.Equal(state.Search("three"), state.Search("THREE"));
        Assert.Equal(state.Search("four"), state.Search("FOUR"));
    }

    [Fact]
    public void EmptyInputCreatesDefaultState()
    {
        HybleBuilder<int> builder = new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance);

        Assert.True(builder.TryCreateWithRetry(Array.Empty<int>(), _intHash, out HybleState<int>? state));
        Assert.NotNull(state);

        Assert.Equal(1u, state.ApproxRange);
        Assert.Equal([0], state.Displacements);
        Assert.Equal(0UL, state.Seed);
        Assert.Equal(0u, state.Search(123));
    }
}