using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BBHash;
using Genbox.FastMPH.CHD;
using Genbox.FastMPH.Hyble;
using Genbox.FastMPH.PTRHash;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class PartialHashTests
{
    private const ulong Seed = 0x517CC1B727220A95UL;

    public delegate PartialBuildStatus PartialHashFunc(ReadOnlySpan<int> keys, Func<int, ulong> hashFunc, ulong seed, out PartialBuildResult<int, IHashState<int>>? result);

    [Theory]
    [MemberData(nameof(GetImpl))]
    public void HashCollisionReturnsPartialAcrossImplementations(string _, PartialHashFunc create)
    {
        int[] values = Enumerable.Range(0, 128).ToArray();

        PartialBuildStatus status = create(values, static _ => 42UL, Seed, out PartialBuildResult<int, IHashState<int>>? result);

        Assert.Equal(PartialBuildStatus.Partial, status);
        Assert.NotNull(result);
        Assert.NotNull(result.State);
        Assert.NotEmpty(result.Remainder);

        HashSet<uint> allIndexes = new HashSet<uint>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            int key = values[i];

            if (!result.Remainder.TryGetValue(key, out uint index))
                index = result.State.Search(key);

            Assert.True(allIndexes.Add(index));
        }
    }

    public static IEnumerable<object[]> GetImpl()
    {
        yield return ["BBHash", new PartialHashFunc(CreateBbHashPartial)];
        yield return ["PTRHash", new PartialHashFunc(CreatePtrHashPartial)];
        yield return ["Hyble", new PartialHashFunc(CreateHyblePartial)];
        yield return ["CHD", new PartialHashFunc(CreateChdPartial)];
    }

    private static PartialBuildStatus CreateBbHashPartial(ReadOnlySpan<int> keys, Func<int, ulong> hashFunc, ulong seed, out PartialBuildResult<int, IHashState<int>>? result)
    {
        BbHashBuilder<int> builder = new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance);
        PartialBuildStatus status = builder.CreatePartial(keys, hashFunc, seed, out PartialBuildResult<int, BbHashMinimalState<int>>? inner);

        result = Adapt(inner);
        return status;
    }

    private static PartialBuildStatus CreatePtrHashPartial(ReadOnlySpan<int> keys, Func<int, ulong> hashFunc, ulong seed, out PartialBuildResult<int, IHashState<int>>? result)
    {
        PtrHashBuilder<int> builder = new PtrHashBuilder<int>(NullLogger<PtrHashBuilder<int>>.Instance);
        PtrHashMinimalSettings settings = new PtrHashMinimalSettings
        {
            Alpha = 0.99,
            Lambda = 3.0,
            BucketFunction = PtrHashBucketFunction.Linear,
            EnableEviction = true,
            Parts = 0,
            TargetKeysPerPart = 32768
        };

        PartialBuildStatus status = builder.CreatePartial(keys, hashFunc, seed, out PartialBuildResult<int, PtrHashMinimalState<int>>? inner, settings);

        result = Adapt(inner);
        return status;
    }

    private static PartialBuildStatus CreateHyblePartial(ReadOnlySpan<int> keys, Func<int, ulong> hashFunc, ulong seed, out PartialBuildResult<int, IHashState<int>>? result)
    {
        HybleBuilder<int> builder = new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance);
        PartialBuildStatus status = builder.CreatePartial(keys, hashFunc, seed, out PartialBuildResult<int, HybleState<int>>? inner);

        result = Adapt(inner);
        return status;
    }

    private static PartialBuildStatus CreateChdPartial(ReadOnlySpan<int> keys, Func<int, ulong> hashFunc, ulong seed, out PartialBuildResult<int, IHashState<int>>? result)
    {
        ChdBuilder<int> builder = new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance);
        PartialBuildStatus status = builder.CreatePartial(keys, hashFunc, seed, out PartialBuildResult<int, ChdState<int>>? inner, new ChdSettings());

        result = Adapt(inner);
        return status;
    }

    private static PartialBuildResult<int, IHashState<int>>? Adapt<TState>(PartialBuildResult<int, TState>? result) where TState : IHashState<int>
    {
        return result == null ? null : new PartialBuildResult<int, IHashState<int>>(result.State, result.Remainder);
    }
}