using System.Collections.Frozen;
using System.Reflection;
using BenchmarkDotNet.Order;
using Genbox.FastMPH;
using Genbox.FastMPH.BDZ;
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
public class IntBenchmarks
{
    private const int Capacity = 100_000;
    private static readonly Func<int, ulong> _intHash = value => unchecked((ulong)value.GetHashCode());

    public delegate object CFunc(ReadOnlySpan<int> data);

    public delegate uint QFunc(int data);

    private int[] _data = null!;
    private int _query;

    [GlobalSetup]
    public void PrepareData()
    {
        // Unique integers from a deterministic RNG
        HashSet<int> seen = new HashSet<int>(Capacity);
        List<int> keys = new List<int>(Capacity);
        while (keys.Count < Capacity)
        {
            int v = Random.Shared.Next();
            if (seen.Add(v))
                keys.Add(v);
        }

        _data = keys.ToArray();
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
    /// Estimate Dictionary&lt;int, uint&gt; memory footprint in bytes.
    /// </summary>
    private static uint DictStateBytes(Dictionary<int, uint> dict)
    {
        int capacity = dict.EnsureCapacity(0);
        return (uint)(112 + 20 * capacity);
    }

    /// <summary>
    /// Estimate FrozenDictionary&lt;int, uint&gt; memory footprint in bytes via reflection.
    /// </summary>
    private static uint FrozenDictStateBytes(FrozenDictionary<int, uint> dict)
    {
        int n = dict.Count;
        int b = GetFrozenHashTableBucketCount(dict);

        if (b > 0)
            return (uint)(144 + 8 * n + 8 * b);

        return (uint)(96 + 8 * n);
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

        Dictionary<int, uint> dictPre = new Dictionary<int, uint>(_data.Length);
        for (int i = 0; i < _data.Length; i++)
            dictPre.Add(_data[i], (uint)i);
        yield return
        [
            "Dict", BitsPerItem(DictStateBytes(dictPre)), new CFunc(data =>
            {
                Dictionary<int, uint> lookup = new Dictionary<int, uint>(data.Length);
                for (int i = 0; i < data.Length; i++)
                    lookup.Add(data[i], (uint)i);
                return lookup;
            })
        ];

        FrozenDictionary<int, uint> frozenPre = dictPre.ToFrozenDictionary();
        yield return
        [
            "FrozenDict", BitsPerItem(FrozenDictStateBytes(frozenPre)), new CFunc(data =>
            {
                Dictionary<int, uint> tmp = new Dictionary<int, uint>(data.Length);
                for (int i = 0; i < data.Length; i++)
                    tmp.Add(data[i], (uint)i);
                return tmp.ToFrozenDictionary();
            })
        ];

        Validator.RequireThat(new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out ChdMinimalState<int>? chdmPre));
        yield return ["CHD_M", BitsPerItem(chdmPre.GetPackedSize()), new CFunc(data => new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new ChdMinimalSettings()))];

        Validator.RequireThat(new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance).TryCreateWithRetry(_data, _intHash, out ChdState<int>? chdPre));
        yield return ["CHD", BitsPerItem(chdPre.GetPackedSize()), new CFunc(data => new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance).TryCreateWithRetry(data, _intHash, out _, new ChdSettings()))];

        Validator.RequireThat(new BdzBuilder<int>(NullLogger<BdzBuilder<int>>.Instance).TryCreateWithRetry(_data, _intHash, out BdzState<int>? bdzPre));
        yield return ["BDZ", BitsPerItem(bdzPre.GetPackedSize()), new CFunc(data => new BdzBuilder<int>(NullLogger<BdzBuilder<int>>.Instance).TryCreateWithRetry(data, _intHash, out _, new BdzSettings()))];

        Validator.RequireThat(new BdzBuilder<int>(NullLogger<BdzBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out BdzMinimalState<int>? bdzmPre));
        yield return ["BDZ_M", BitsPerItem(bdzmPre.GetPackedSize()), new CFunc(data => new BdzBuilder<int>(NullLogger<BdzBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new BdzMinimalSettings()))];

        Validator.RequireThat(new BmzBuilder<int>(NullLogger<BmzBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out BmzMinimalState<int>? bmzPre));
        yield return ["BMZ_M", BitsPerItem(bmzPre.GetPackedSize()), new CFunc(data => new BmzBuilder<int>(NullLogger<BmzBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new BmzMinimalSettings()))];

        Validator.RequireThat(new ChmBuilder<int>(NullLogger<ChmBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out ChmMinimalState<int>? chmPre));
        yield return ["CHM_M", BitsPerItem(chmPre.GetPackedSize()), new CFunc(data => new ChmBuilder<int>(NullLogger<ChmBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new ChmMinimalSettings()))];

        Validator.RequireThat(new FchBuilder<int>(NullLogger<FchBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out FchMinimalState<int>? fchPre));
        yield return ["FCH_M", BitsPerItem(fchPre.GetPackedSize()), new CFunc(data => new FchBuilder<int>(NullLogger<FchBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new FchMinimalSettings()))];

        Validator.RequireThat(new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out BbHashMinimalState<int>? bbPre));
        yield return ["BB_M", BitsPerItem(bbPre.GetPackedSize()), new CFunc(data => new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new BbHashMinimalSettings()))];

        Validator.RequireThat(new PtrHashBuilder<int>(NullLogger<PtrHashBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out PtrHashMinimalState<int>? ptrPre));
        yield return ["PTR_M", BitsPerItem(ptrPre.GetPackedSize()), new CFunc(data => new PtrHashBuilder<int>(NullLogger<PtrHashBuilder<int>>.Instance).TryCreateMinimalWithRetry(data, _intHash, out _, new PtrHashMinimalSettings()))];

        Validator.RequireThat(new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance).TryCreateWithRetry(_data, _intHash, out HybleState<int>? hyblePre));
        yield return ["Hyble", BitsPerItem(hyblePre.GetPackedSize()), new CFunc(data => new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance).TryCreateWithRetry(data, _intHash, out _, new HybleSettings()))];
    }

    public IEnumerable<object[]> GetQueryImpl()
    {
        PrepareData();

        Dictionary<int, uint> lookup = new Dictionary<int, uint>(_data.Length);
        for (uint i = 0; i < _data.Length; i++)
            lookup.Add(_data[i], i);
        yield return ["Dict", BitsPerItem(DictStateBytes(lookup)), new QFunc(data => lookup.GetValueOrDefault(data, 0u))];

        FrozenDictionary<int, uint> frozenDict = lookup.ToFrozenDictionary();
        yield return ["FrozenDict", BitsPerItem(FrozenDictStateBytes(frozenDict)), new QFunc(data => frozenDict.GetValueOrDefault(data, 0u))];

        Validator.RequireThat(new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out ChdMinimalState<int>? chdmState));
        yield return ["CHD_M", BitsPerItem(chdmState.GetPackedSize()), new QFunc(data => chdmState.Search(data))];

        Validator.RequireThat(new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance).TryCreateWithRetry(_data, _intHash, out ChdState<int>? chdState));
        yield return ["CHD", BitsPerItem(chdState.GetPackedSize()), new QFunc(data => chdState.Search(data))];

        Validator.RequireThat(new BdzBuilder<int>(NullLogger<BdzBuilder<int>>.Instance).TryCreateWithRetry(_data, _intHash, out BdzState<int>? bdzState));
        yield return ["BDZ", BitsPerItem(bdzState.GetPackedSize()), new QFunc(data => bdzState.Search(data))];

        Validator.RequireThat(new BdzBuilder<int>(NullLogger<BdzBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out BdzMinimalState<int>? bdzmState));
        yield return ["BDZ_M", BitsPerItem(bdzmState.GetPackedSize()), new QFunc(data => bdzmState.Search(data))];

        Validator.RequireThat(new BmzBuilder<int>(NullLogger<BmzBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out BmzMinimalState<int>? bmzState));
        yield return ["BMZ_M", BitsPerItem(bmzState.GetPackedSize()), new QFunc(data => bmzState.Search(data))];

        Validator.RequireThat(new ChmBuilder<int>(NullLogger<ChmBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out ChmMinimalState<int>? chmState));
        yield return ["CHM_M", BitsPerItem(chmState.GetPackedSize()), new QFunc(data => chmState.Search(data))];

        Validator.RequireThat(new FchBuilder<int>(NullLogger<FchBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out FchMinimalState<int>? fchState));
        yield return ["FCH_M", BitsPerItem(fchState.GetPackedSize()), new QFunc(data => fchState.Search(data))];

        Validator.RequireThat(new BbHashBuilder<int>(NullLogger<BbHashBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out BbHashMinimalState<int>? bbState));
        yield return ["BB_M", BitsPerItem(bbState.GetPackedSize()), new QFunc(data => bbState.Search(data))];

        Validator.RequireThat(new PtrHashBuilder<int>(NullLogger<PtrHashBuilder<int>>.Instance).TryCreateMinimalWithRetry(_data, _intHash, out PtrHashMinimalState<int>? ptrState));
        yield return ["PTR_M", BitsPerItem(ptrState.GetPackedSize()), new QFunc(data => ptrState.Search(data))];

        Validator.RequireThat(new HybleBuilder<int>(NullLogger<HybleBuilder<int>>.Instance).TryCreateWithRetry(_data, _intHash, out HybleState<int>? hybleState));
        yield return ["Hyble", BitsPerItem(hybleState.GetPackedSize()), new QFunc(data => hybleState.Search(data))];
    }
}