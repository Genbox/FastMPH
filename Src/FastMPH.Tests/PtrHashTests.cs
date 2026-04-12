using Genbox.FastMPH.PTRHash;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class PtrHashTests
{
    private static readonly int[] _randomSizes = [0, 1, 2, 3, 10, 30, 100, 300, 1_000, 3_000, 10_000];
    private static readonly int[] _multipleSizes = [0, 1, 2, 10, 100, 300, 1_000, 3_000];
    private static readonly Func<ulong, uint> _strongUlongHash = value => unchecked((uint)Mix64(value));
    private static readonly Func<string, uint> _ordinalIgnoreCaseHash = value => unchecked((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(value));

    [Theory]
    [InlineData(PtrHashBucketFunction.Linear)]
    [InlineData(PtrHashBucketFunction.SquareEps)]
    [InlineData(PtrHashBucketFunction.CubicEps)]
    public void ConstructRandomKeysMapsIntoMinimalRange(PtrHashBucketFunction bucketFunction)
    {
        PtrHashBuilder<ulong> builder = new PtrHashBuilder<ulong>(NullLogger<PtrHashBuilder<ulong>>.Instance);

        foreach (int size in _randomSizes)
        {
            ulong[] keys = GeneratePseudoRandomKeys(size);
            PtrHashMinimalSettings settings = CreateSettings(bucketFunction);

            Assert.True(builder.TryCreateMinimal(keys, _strongUlongHash, out PtrHashMinimalState<ulong>? state, settings));
            Assert.NotNull(state);

            Assert.Equal((uint)size, state.NumKeys);
            Assert.True(state.NumParts >= 1);
            Assert.Equal(state.NumParts * state.SlotsPerPart, state.NumSlots);
            Assert.Equal(state.NumParts * state.BucketsPerPart, (uint)state.Pilots.Length);
            Assert.Equal(bucketFunction, state.BucketFunction);

            AssertMinimalPerfect(keys, state);
        }
    }

    [Fact]
    public void ConstructMultiplesMapsIntoMinimalRange()
    {
        PtrHashBuilder<ulong> builder = new PtrHashBuilder<ulong>(NullLogger<PtrHashBuilder<ulong>>.Instance);

        foreach (ulong multiplier in new[] { 1UL, 1UL << 40, 1_000_000_000_000UL, Pow(3UL, 23) })
        {
            foreach (int size in _multipleSizes)
            {
                ulong[] keys = new ulong[size];
                for (int i = 0; i < size; i++)
                    keys[i] = unchecked(multiplier * (ulong)i);

                PtrHashMinimalSettings settings = CreateSettings(PtrHashBucketFunction.Linear);
                Assert.True(builder.TryCreateMinimal(keys, _strongUlongHash, out PtrHashMinimalState<ulong>? state, settings));
                Assert.NotNull(state);

                AssertMinimalPerfect(keys, state);
            }
        }
    }

    [Fact]
    public void RoundtripPreservesSearchResults()
    {
        ulong[] keys = GeneratePseudoRandomKeys(5_000);
        PtrHashBuilder<ulong> builder = new PtrHashBuilder<ulong>(NullLogger<PtrHashBuilder<ulong>>.Instance);
        PtrHashMinimalSettings settings = CreateSettings(PtrHashBucketFunction.Linear);

        Assert.True(builder.TryCreateMinimal(keys, _strongUlongHash, out PtrHashMinimalState<ulong>? state, settings));
        Assert.NotNull(state);

        byte[] packed = new byte[state.GetPackedSize()];
        state.Pack(packed);

        PtrHashMinimalState<ulong> unpacked = PtrHashMinimalState<ulong>.Unpack(packed, _strongUlongHash);

        Assert.Equal(state.NumKeys, unpacked.NumKeys);
        Assert.Equal(state.NumSlots, unpacked.NumSlots);
        Assert.Equal(state.NumParts, unpacked.NumParts);
        Assert.Equal(state.SlotsPerPart, unpacked.SlotsPerPart);
        Assert.Equal(state.BucketsPerPart, unpacked.BucketsPerPart);
        Assert.Equal(state.BucketFunction, unpacked.BucketFunction);
        Assert.Equal(state.Seed, unpacked.Seed);

        for (int i = 0; i < keys.Length; i++)
            Assert.Equal(state.Search(keys[i]), unpacked.Search(keys[i]));
    }

    [Fact]
    public void IntegerKeyTypes()
    {
        AssertSingleValueType((byte)7);
        AssertSingleValueType((ushort)7);
        AssertSingleValueType((uint)7);
        AssertSingleValueType(7UL);
        AssertSingleValueType((sbyte)7);
        AssertSingleValueType((short)7);
        AssertSingleValueType(7);
        AssertSingleValueType(7L);
    }

    [Fact]
    public void StringHashSupport()
    {
        string[] keys = ["alpha", "beta", "gamma", "delta"];
        PtrHashBuilder<string> builder = new PtrHashBuilder<string>(NullLogger<PtrHashBuilder<string>>.Instance);

        Assert.True(builder.TryCreateMinimal(keys, _ordinalIgnoreCaseHash, out PtrHashMinimalState<string>? state, CreateSettings(PtrHashBucketFunction.Linear)));
        Assert.NotNull(state);

        Assert.Equal(state.Search("alpha"), state.Search("ALPHA"));
        Assert.Equal(state.Search("beta"), state.Search("BeTa"));
        Assert.Equal(state.Search("gamma"), state.Search("GAMMA"));
        Assert.Equal(state.Search("delta"), state.Search("DELTA"));
    }

    private static void AssertSingleValueType<T>(T value) where T : notnull
    {
        T[] keys = [value];
        PtrHashBuilder<T> builder = new PtrHashBuilder<T>(NullLogger<PtrHashBuilder<T>>.Instance);

        Assert.True(builder.TryCreateMinimal(keys, GetDefaultHash<T>(), out PtrHashMinimalState<T>? state, CreateSettings(PtrHashBucketFunction.Linear)));
        Assert.NotNull(state);
        Assert.Equal(0u, state.Search(value));
    }

    private static void AssertMinimalPerfect(ulong[] keys, PtrHashMinimalState<ulong> state)
    {
        if (keys.Length == 0)
        {
            Assert.Equal(0u, state.Search(0));
            return;
        }

        HashSet<uint> seen = new HashSet<uint>(keys.Length);

        for (int i = 0; i < keys.Length; i++)
        {
            uint idx = state.Search(keys[i]);
            Assert.True(idx < (uint)keys.Length);
            Assert.True(seen.Add(idx));
        }
    }

    private static PtrHashMinimalSettings CreateSettings(PtrHashBucketFunction bucketFunction)
    {
        return new PtrHashMinimalSettings
        {
            Alpha = 0.99,
            Lambda = bucketFunction == PtrHashBucketFunction.Linear ? 3.0 : 3.5,
            Iterations = 80,
            BucketFunction = bucketFunction,
            EnableEviction = true,
            Parts = 0,
            TargetKeysPerPart = 32_768
        };
    }

    private static ulong[] GeneratePseudoRandomKeys(int count)
    {
        ulong[] result = new ulong[count];

        for (int i = 0; i < count; i++)
            result[i] = Mix64((ulong)i + 1UL);

        return result;
    }

    private static ulong Mix64(ulong x)
    {
        unchecked
        {
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            return x;
        }
    }

    private static ulong Pow(ulong value, int exp)
    {
        ulong result = 1;
        for (int i = 0; i < exp; i++)
            result *= value;

        return result;
    }

    private static Func<T, uint> GetDefaultHash<T>() where T : notnull => value => unchecked((uint)value.GetHashCode());
}
