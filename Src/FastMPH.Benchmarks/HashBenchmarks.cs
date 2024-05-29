using BenchmarkDotNet.Order;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Benchmarks.Misc;
using Genbox.FastMPH.BMZ;
using Genbox.FastMPH.CHD;
using Genbox.FastMPH.CHM;
using Genbox.FastMPH.FCH;
using Genbox.FastMPH.Internals;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Benchmarks;

[HideColumns("create", "search", "Error", "StdDev")]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class HashBenchmarks
{
    private string[] _data;
    private string _query;

    [GlobalSetup]
    public void PrepareData()
    {
        _data = RandomHelper.GetRandomStrings(10, 1_000).ToArray();
        _query = _data[_data.Length - 1];
    }

    [Benchmark]
    [ArgumentsSource(nameof(GetConstructImpl))]
    public void Construct(string name, CFunc create) => create(_data);

    [Benchmark]
    [ArgumentsSource(nameof(GetQueryImpl))]
    public uint Query(string name, QFunc search) => search(_query);

    public IEnumerable<object[]> GetConstructImpl()
    {
        yield return
        [
            "Dict", new CFunc(data =>
            {
                Dictionary<string, uint> lookup = new Dictionary<string, uint>(data.Length);

                for (int i = 0; i < data.Length; i++)
                    lookup.Add(data[i], (uint)i);
            })
        ];

        yield return ["CHD_M", new CFunc(data => new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreateMinimal(data, out _, new ChdMinimalSettings()))];
        yield return ["CHD", new CFunc(data => new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreate(data, out _, new ChdSettings()))];
        yield return ["BDZ", new CFunc(data => new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreate(data, out _, new BdzSettings()))];
        yield return ["BDZ_M", new CFunc(data => new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreateMinimal(data, out _, new BdzMinimalSettings()))];
        yield return ["BDZ_M", new CFunc(data => new BmzBuilder<string>(NullLogger<BmzBuilder<string>>.Instance).TryCreateMinimal(data, out _, new BmzMinimalSettings()))];
        yield return ["CHM_M", new CFunc(data => new ChmBuilder<string>(NullLogger<ChmBuilder<string>>.Instance).TryCreateMinimal(data, out _, new ChmMinimalSettings()))];
        yield return ["FCH_M", new CFunc(data => new FchBuilder<string>(NullLogger<FchBuilder<string>>.Instance).TryCreateMinimal(data, out _, new FchMinimalSettings()))];
    }

    public IEnumerable<object[]> GetQueryImpl()
    {
        PrepareData();

        Dictionary<string, uint> lookup = new Dictionary<string, uint>(_data.Length);

        for (uint i = 0; i < _data.Length; i++)
            lookup.Add(_data[i], i);

        yield return ["Dict", new QFunc(data => lookup.GetValueOrDefault(data, 0u))];

        Validator.RequireThat(new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreateMinimal(_data, out var state6));
        yield return ["CHD_M", new QFunc(data => state6.Search(data))];

        Validator.RequireThat(new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreate(_data, out var state7));
        yield return ["CHD", new QFunc(data => state7.Search(data))];

        Validator.RequireThat(new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreate(_data, out var state1));
        yield return ["BDZ", new QFunc(data => state1.Search(data))];

        Validator.RequireThat(new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreateMinimal(_data, out var state2));
        yield return ["BDZ_M", new QFunc(data => state2.Search(data))];

        Validator.RequireThat(new BmzBuilder<string>(NullLogger<BmzBuilder<string>>.Instance).TryCreateMinimal(_data, out var state3));
        yield return ["BMZ_M", new QFunc(data => state3.Search(data))];

        Validator.RequireThat(new ChmBuilder<string>(NullLogger<ChmBuilder<string>>.Instance).TryCreateMinimal(_data, out var state4));
        yield return ["CHM_M", new QFunc(data => state4.Search(data))];

        Validator.RequireThat(new FchBuilder<string>(NullLogger<FchBuilder<string>>.Instance).TryCreateMinimal(_data, out var state5));
        yield return ["FCH_M", new QFunc(data => state5.Search(data))];
    }

    public delegate void CFunc(ReadOnlySpan<string> data);
    public delegate uint QFunc(string data);
}