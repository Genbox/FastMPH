using System.IO.Hashing;
using System.Text;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.BMZ;
using Genbox.FastMPH.CHD;
using Genbox.FastMPH.CHM;
using Genbox.FastMPH.FCH;
using Genbox.FastMPH.BBHash;
using Genbox.FastMPH.Hyble;
using Genbox.FastMPH.Internals.Helpers;
using Genbox.FastMPH.PTRHash;
using Genbox.FastMPH.Tests.Misc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class HashTests(ITestOutputHelper output)
{
    public delegate bool HashFunc<TState>(ReadOnlySpan<byte[]> data, out TState? result);

    private static ulong ByteArrayHash(byte[] value, ulong seed) => unchecked(XxHash3.HashToUInt64(value, (long)seed));

    [Theory]
    [MemberData(nameof(GetImpl))]
    public void PerfectHashTest<TState>(HashFunc<TState?> create, Func<byte[], TState> unpack) where TState : IQueryState<byte[]>
    {
        byte[][] values = StringHelper.GetRandomStrings(10, 100).DistinctBy(x => x).Select(x => Encoding.UTF8.GetBytes(x)).ToArray();

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

        output.WriteLine($"{state.GetType().Name} packed size: {size}");

        byte[] packed = new byte[size];
        state.Pack(packed);

        IQueryState<byte[]> unpacked = unpack(packed);
        Assert.Equivalent(state, unpacked);

        //Test if we can query the unpacked version. It should give us only already known values.
        for (int i = 0; i < values.Length; i++)
        {
            uint index = unpacked.Search(values[i]);
            Assert.False(uniq.Add(index));
        }
    }

    public static IEnumerable<object[]> GetImpl()
    {
        yield return [new HashFunc<BdzState<byte[]>>((data, out state) => new BdzBuilder<byte[]>(NullLogger<BdzBuilder<byte[]>>.Instance).TryCreateWithRetry(data, ByteArrayHash, out state, new BdzSettings())), (byte[] data) => BdzState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<ChdState<byte[]>>((data, out state) => new ChdBuilder<byte[]>(NullLogger<ChdBuilder<byte[]>>.Instance).TryCreateWithRetry(data, ByteArrayHash, out state, new ChdSettings())), (byte[] data) => ChdState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<BdzMinimalState<byte[]>>((data, out state) => new BdzBuilder<byte[]>(NullLogger<BdzBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new BdzMinimalSettings())), (byte[] data) => BdzMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<BmzMinimalState<byte[]>>((data, out state) => new BmzBuilder<byte[]>(NullLogger<BmzBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new BmzMinimalSettings())), (byte[] data) => BmzMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<ChdMinimalState<byte[]>>((data, out state) => new ChdBuilder<byte[]>(NullLogger<ChdBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new ChdMinimalSettings())), (byte[] data) => ChdMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<ChmMinimalState<byte[]>>((data, out state) => new ChmBuilder<byte[]>(NullLogger<ChmBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new ChmMinimalSettings())), (byte[] data) => ChmMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<FchMinimalState<byte[]>>((data, out state) => new FchBuilder<byte[]>(NullLogger<FchBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new FchMinimalSettings())), (byte[] data) => FchMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<BbHashMinimalState<byte[]>>((data, out state) => new BbHashBuilder<byte[]>(NullLogger<BbHashBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new BbHashMinimalSettings())), (byte[] data) => BbHashMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<PtrHashMinimalState<byte[]>>((data, out state) => new PtrHashBuilder<byte[]>(NullLogger<PtrHashBuilder<byte[]>>.Instance).TryCreateMinimalWithRetry(data, ByteArrayHash, out state, new PtrHashMinimalSettings())), (byte[] data) => PtrHashMinimalState<byte[]>.Unpack(data, ByteArrayHash)];
        yield return [new HashFunc<HybleState<byte[]>>((data, out state) => new HybleBuilder<byte[]>(NullLogger<HybleBuilder<byte[]>>.Instance).TryCreateWithRetry(data, ByteArrayHash, out state, new HybleSettings())), (byte[] data) => HybleState<byte[]>.Unpack(data, ByteArrayHash)];
    }
}