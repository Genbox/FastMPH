using System.Collections.Frozen;
using System.IO.Hashing;
using System.Reflection;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Order;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Benchmarks.Misc;
using Genbox.FastMPH.BBHash;
using Genbox.FastMPH.BMZ;
using Genbox.FastMPH.CHD;
using Genbox.FastMPH.CHM;
using Genbox.FastMPH.FCH;
using Genbox.FastMPH.Hyble;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.PTRHash;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Benchmarks;

[HideColumns("create", "search", "Gen0", "Gen1", "Gen2")]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class StringBenchmarks
{
    public delegate object CFunc(ReadOnlySpan<string> data);

    public delegate uint QFunc(string data);

    private string[] _data = null!;
    private string _query = null!;

    [GlobalSetup]
    public void PrepareData()
    {
        _data = StringHelper.GetRandomStrings(10, 1_000).ToArray();
        _query = _data[_data.Length - 1];
    }

    [Benchmark]
    [ArgumentsSource(nameof(GetConstructImpl))]
    public object Construct(string name, double bitsPerItem, CFunc create) => create(_data);

    [Benchmark]
    [ArgumentsSource(nameof(GetQueryImpl))]
    public uint Query(string name, double bitsPerItem, QFunc search) => search(_query);

    /// <summary>
    /// Convert a byte count to bits per item, rounded to 2 decimal places.
    /// </summary>
    private double BitsPerItem(uint stateBytes) => Math.Round(stateBytes * 8.0 / _data.Length, 2);

    /// <summary>
    /// Estimate Dictionary&lt;string, uint&gt; memory footprint in bytes (excluding string objects).
    /// </summary>
    private static uint DictStateBytes(Dictionary<string, uint> dict)
    {
        int capacity = dict.EnsureCapacity(0);
        return (uint)(112 + 28 * capacity);
    }

    /// <summary>
    /// Estimate FrozenDictionary&lt;string, uint&gt; memory footprint in bytes (excluding string objects).
    /// </summary>
    private static uint FrozenDictStateBytes(FrozenDictionary<string, uint> dict)
    {
        int n = dict.Count;
        int b = GetFrozenHashTableBucketCount(dict);

        if (b > 0)
            return (uint)(168 + 16 * n + 8 * b);

        return (uint)(120 + 12 * n);
    }

    private static int GetFrozenHashTableBucketCount(object frozen)
    {
        Type type = frozen.GetType();

        FieldInfo? hashTableField = null;
        for (Type? t = type; t != null && hashTableField == null; t = t.BaseType)
            hashTableField = t.GetField("_hashTable", BindingFlags.Instance | BindingFlags.NonPublic);

        if (hashTableField != null)
        {
            object? hashTable = hashTableField.GetValue(frozen);
            if (hashTable != null)
            {
                FieldInfo? bucketsField = hashTable.GetType().GetField("_buckets", BindingFlags.Instance | BindingFlags.NonPublic);
                if (bucketsField != null && bucketsField.GetValue(hashTable) is Array buckets)
                    return buckets.Length;
            }
        }

        return 0;
    }

    public IEnumerable<object[]> GetConstructImpl()
    {
        PrepareData();

        Dictionary<string, uint> dictPre = new Dictionary<string, uint>(_data.Length);
        for (int i = 0; i < _data.Length; i++)
            dictPre.Add(_data[i], (uint)i);
        yield return
        [
            "Dict", BitsPerItem(DictStateBytes(dictPre)), new CFunc(data =>
            {
                Dictionary<string, uint> lookup = new Dictionary<string, uint>(data.Length);
                for (int i = 0; i < data.Length; i++)
                    lookup.Add(data[i], (uint)i);
                return lookup;
            })
        ];

        FrozenDictionary<string, uint> frozenPre = dictPre.ToFrozenDictionary();
        yield return
        [
            "FrozenDict", BitsPerItem(FrozenDictStateBytes(frozenPre)), new CFunc(data =>
            {
                Dictionary<string, uint> tmp = new Dictionary<string, uint>(data.Length);
                for (int i = 0; i < data.Length; i++)
                    tmp.Add(data[i], (uint)i);
                return tmp.ToFrozenDictionary();
            })
        ];

        Validator.RequireThat(new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreateMinimal(_data, out ChdMinimalState<string>? chdmPre));
        yield return ["CHD_M", BitsPerItem(chdmPre.GetPackedSize()), new CFunc(data => new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreateMinimal(data, out _, new ChdMinimalSettings()))];

        Validator.RequireThat(new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreate(_data, out ChdState<string>? chdPre));
        yield return ["CHD", BitsPerItem(chdPre.GetPackedSize()), new CFunc(data => new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreate(data, out _, new ChdSettings()))];

        Validator.RequireThat(new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreate(_data, out BdzState<string>? bdzPre));
        yield return ["BDZ", BitsPerItem(bdzPre.GetPackedSize()), new CFunc(data => new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreate(data, out _, new BdzSettings()))];

        Validator.RequireThat(new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreateMinimal(_data, out BdzMinimalState<string>? bdzmPre));
        yield return ["BDZ_M", BitsPerItem(bdzmPre.GetPackedSize()), new CFunc(data => new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreateMinimal(data, out _, new BdzMinimalSettings()))];

        Validator.RequireThat(new BmzBuilder<string>(NullLogger<BmzBuilder<string>>.Instance).TryCreateMinimal(_data, out BmzMinimalState<string>? bmzPre));
        yield return ["BMZ_M", BitsPerItem(bmzPre.GetPackedSize()), new CFunc(data => new BmzBuilder<string>(NullLogger<BmzBuilder<string>>.Instance).TryCreateMinimal(data, out _, new BmzMinimalSettings()))];

        Validator.RequireThat(new ChmBuilder<string>(NullLogger<ChmBuilder<string>>.Instance).TryCreateMinimal(_data, out ChmMinimalState<string>? chmPre));
        yield return ["CHM_M", BitsPerItem(chmPre.GetPackedSize()), new CFunc(data => new ChmBuilder<string>(NullLogger<ChmBuilder<string>>.Instance).TryCreateMinimal(data, out _, new ChmMinimalSettings()))];

        Validator.RequireThat(new FchBuilder<string>(NullLogger<FchBuilder<string>>.Instance).TryCreateMinimal(_data, out FchMinimalState<string>? fchPre));
        yield return ["FCH_M", BitsPerItem(fchPre.GetPackedSize()), new CFunc(data => new FchBuilder<string>(NullLogger<FchBuilder<string>>.Instance).TryCreateMinimal(data, out _, new FchMinimalSettings()))];

        Validator.RequireThat(new BbHashBuilder<string>(NullLogger<BbHashBuilder<string>>.Instance).TryCreateMinimal(_data, out BbHashMinimalState<string>? bbPre));
        yield return ["BB_M", BitsPerItem(bbPre.GetPackedSize()), new CFunc(data => new BbHashBuilder<string>(NullLogger<BbHashBuilder<string>>.Instance).TryCreateMinimal(data, out _, new BbHashMinimalSettings()))];

        Validator.RequireThat(new HybleBuilder<string>(NullLogger<HybleBuilder<string>>.Instance).TryCreate(_data, out HybleState<string>? hyblePre));
        yield return ["HYBLE", BitsPerItem(hyblePre.GetPackedSize()), new CFunc(data => new HybleBuilder<string>(NullLogger<HybleBuilder<string>>.Instance).TryCreate(data, out _, new HybleSettings()))];
    }

    public IEnumerable<object[]> GetQueryImpl()
    {
        PrepareData();

        Dictionary<string, uint> lookup = new Dictionary<string, uint>(_data.Length);
        for (uint i = 0; i < _data.Length; i++)
            lookup.Add(_data[i], i);
        yield return ["Dict", BitsPerItem(DictStateBytes(lookup)), new QFunc(data => lookup.GetValueOrDefault(data, 0u))];

        FrozenDictionary<string, uint> frozenDict = lookup.ToFrozenDictionary();
        yield return ["FrozenDict", BitsPerItem(FrozenDictStateBytes(frozenDict)), new QFunc(data => frozenDict.GetValueOrDefault(data, 0u))];

        Validator.RequireThat(new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreateMinimal(_data, out ChdMinimalState<string>? state6));
        yield return ["CHD_M", BitsPerItem(state6.GetPackedSize()), new QFunc(data => state6.Search(data))];

        Validator.RequireThat(new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance).TryCreate(_data, out ChdState<string>? state7));
        yield return ["CHD", BitsPerItem(state7.GetPackedSize()), new QFunc(data => state7.Search(data))];

        Validator.RequireThat(new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreate(_data, out BdzState<string>? state1));
        yield return ["BDZ", BitsPerItem(state1.GetPackedSize()), new QFunc(data => state1.Search(data))];

        Validator.RequireThat(new BdzBuilder<string>(NullLogger<BdzBuilder<string>>.Instance).TryCreateMinimal(_data, out BdzMinimalState<string>? state2));
        yield return ["BDZ_M", BitsPerItem(state2.GetPackedSize()), new QFunc(data => state2.Search(data))];

        Validator.RequireThat(new BmzBuilder<string>(NullLogger<BmzBuilder<string>>.Instance).TryCreateMinimal(_data, out BmzMinimalState<string>? state3));
        yield return ["BMZ_M", BitsPerItem(state3.GetPackedSize()), new QFunc(data => state3.Search(data))];

        Validator.RequireThat(new ChmBuilder<string>(NullLogger<ChmBuilder<string>>.Instance).TryCreateMinimal(_data, out ChmMinimalState<string>? state4));
        yield return ["CHM_M", BitsPerItem(state4.GetPackedSize()), new QFunc(data => state4.Search(data))];

        Validator.RequireThat(new FchBuilder<string>(NullLogger<FchBuilder<string>>.Instance).TryCreateMinimal(_data, out FchMinimalState<string>? state5));
        yield return ["FCH_M", BitsPerItem(state5.GetPackedSize()), new QFunc(data => state5.Search(data))];

        Validator.RequireThat(new BbHashBuilder<string>(NullLogger<BbHashBuilder<string>>.Instance).TryCreateMinimal(_data, out BbHashMinimalState<string>? bbState));
        yield return ["BB_M", BitsPerItem(bbState.GetPackedSize()), new QFunc(data => bbState.Search(data))];

        Validator.RequireThat(new PtrHashBuilder<string>(NullLogger<PtrHashBuilder<string>>.Instance).TryCreateMinimal(_data, out PtrHashMinimalState<string>? ptrState));
        yield return ["PTR_M", BitsPerItem(ptrState.GetPackedSize()), new QFunc(data => ptrState.Search(data))];

        Validator.RequireThat(new HybleBuilder<string>(NullLogger<HybleBuilder<string>>.Instance).TryCreate(_data, out HybleState<string>? hybleState));
        yield return ["HYBLE", BitsPerItem(hybleState.GetPackedSize()), new QFunc(data => hybleState.Search(data))];
    }
}