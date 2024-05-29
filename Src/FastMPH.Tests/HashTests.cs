using System.Text;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.BMZ;
using Genbox.FastMPH.CHD;
using Genbox.FastMPH.CHM;
using Genbox.FastMPH.FCH;
using Genbox.FastMPH.Tests.Misc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class HashTests
{
    public delegate bool HashFunc<TState>(ReadOnlySpan<byte[]> data, out TState? result);

    [Theory]
    [MemberData(nameof(GetImpl))]
    public void PerfectHashTest<TState>(HashFunc<TState?> create, Func<byte[], TState> unpack) where TState : IHashState<byte[]>
    {
        byte[][] values = RandomHelper.GetRandomStrings(10, 100).DistinctBy(x => x).Select(x => Encoding.UTF8.GetBytes(x)).ToArray();

        Assert.True(create(values, out TState? state));
        Assert.NotNull(state);

        //Test uniqueness
        HashSet<uint> uniq = new HashSet<uint>();

        for (int i = 0; i < values.Length; i++)
        {
            uint index = state.Search(values[i]);
            Assert.True(uniq.Add(index));
        }

        //Test packing
        uint size = state.GetPackedSize();
        Assert.True(size > 0);

        byte[] packed = new byte[size];
        state.Pack(packed);

        IHashState<byte[]> unpacked = unpack(packed);
        Assert.Equivalent(state, unpacked);

        for (int i = 0; i < values.Length; i++)
        {
            uint index = unpacked.Search(values[i]);
            Assert.False(uniq.Add(index));
        }
    }

    public static IEnumerable<object[]> GetImpl()
    {
        yield return [new HashFunc<BdzState<byte[]>>((ReadOnlySpan<byte[]> data, out BdzState<byte[]>? state) => new BdzBuilder<byte[]>(NullLogger<BdzBuilder<byte[]>>.Instance).TryCreate(data, out state, new BdzSettings())), (byte[] data) => BdzState<byte[]>.Unpack(data)];
        yield return [new HashFunc<ChdState<byte[]>>((ReadOnlySpan<byte[]> data, out ChdState<byte[]>? state) => new ChdBuilder<byte[]>(NullLogger<ChdBuilder<byte[]>>.Instance).TryCreate(data, out state, new ChdSettings())), (byte[] data) => ChdState<byte[]>.Unpack(data)];
        yield return [new HashFunc<BdzMinimalState<byte[]>>((ReadOnlySpan<byte[]> data, out BdzMinimalState<byte[]>? state) => new BdzBuilder<byte[]>(NullLogger<BdzBuilder<byte[]>>.Instance).TryCreateMinimal(data, out state, new BdzMinimalSettings())), (byte[] data) => BdzMinimalState<byte[]>.Unpack(data)];
        yield return [new HashFunc<BmzMinimalState<byte[]>>((ReadOnlySpan<byte[]> data, out BmzMinimalState<byte[]>? state) => new BmzBuilder<byte[]>(NullLogger<BmzBuilder<byte[]>>.Instance).TryCreateMinimal(data, out state, new BmzMinimalSettings())), (byte[] data) => BmzMinimalState<byte[]>.Unpack(data)];
        yield return [new HashFunc<ChdMinimalState<byte[]>>((ReadOnlySpan<byte[]> data, out ChdMinimalState<byte[]>? state) => new ChdBuilder<byte[]>(NullLogger<ChdBuilder<byte[]>>.Instance).TryCreateMinimal(data, out state, new ChdMinimalSettings())), (byte[] data) => ChdMinimalState<byte[]>.Unpack(data)];
        yield return [new HashFunc<ChmMinimalState<byte[]>>((ReadOnlySpan<byte[]> data, out ChmMinimalState<byte[]>? state) => new ChmBuilder<byte[]>(NullLogger<ChmBuilder<byte[]>>.Instance).TryCreateMinimal(data, out state, new ChmMinimalSettings())), (byte[] data) => ChmMinimalState<byte[]>.Unpack(data)];
        yield return [new HashFunc<FchMinimalState<byte[]>>((ReadOnlySpan<byte[]> data, out FchMinimalState<byte[]>? state) => new FchBuilder<byte[]>(NullLogger<FchBuilder<byte[]>>.Instance).TryCreateMinimal(data, out state, new FchMinimalSettings())), (byte[] data) => FchMinimalState<byte[]>.Unpack(data)];
    }
}