using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.PTRHash;

/// <summary>Settings for the PTRHash-style minimal perfect hash function.</summary>
[PublicAPI]
public sealed class PtrHashMinimalSettings : HashSettings
{
    /// <summary>
    /// Slot load factor. Values closer to 1.0 use less space but are harder to construct. Default is 0.99.
    /// </summary>
    public double Alpha
    {
        get;
        set
        {
            Validator.RequireThat(value is > 0.0 and < 1.0);
            field = value;
        }
    } = 0.99;

    /// <summary>
    /// Average keys per bucket. Higher values lower bucket count but can hurt construction reliability. Default is 3.0.
    /// </summary>
    public double Lambda
    {
        get;
        set
        {
            Validator.RequireThat(value > 0.0);
            field = value;
        }
    } = 3.0;

    /// <summary>
    /// Create settings with PTRHash defaults.
    /// </summary>
    public PtrHashMinimalSettings() => Iterations = 20;

    /// <summary>
    /// Maximum number of pilot values to try for each bucket. Default is 256, which means pilot values [0,255].
    /// </summary>
    public uint MaxPilot
    {
        get;
        set
        {
            Validator.RequireThat(value is > 0 and <= 256);
            field = value;
        }
    } = 256;

    /// <summary>
    /// Number of partition parts. Set to 0 to auto-select based on <see cref="TargetKeysPerPart" />.
    /// Higher values can improve construction throughput for large key sets.
    /// Default is 0 (auto).
    /// </summary>
    public uint Parts { get; set; }

    /// <summary>
    /// Target number of keys per part when <see cref="Parts" /> is 0.
    /// Lower values create more parts and generally improve cache locality.
    /// Default is 262144.
    /// </summary>
    public uint TargetKeysPerPart
    {
        get;
        set
        {
            Validator.RequireThat(value > 0);
            field = value;
        }
    } = 262144;

    /// <summary>
    /// Bucket function used to map hashes into buckets. Default is <see cref="PtrHashBucketFunction.Linear" />.
    /// </summary>
    public PtrHashBucketFunction BucketFunction { get; set; } = PtrHashBucketFunction.Linear;

    /// <summary>
    /// Enable eviction-based placement when no direct pilot can be found.
    /// This improves construction reliability and often allows denser layouts. Default is true.
    /// </summary>
    public bool EnableEviction { get; set; } = true;

    /// <summary>
    /// Maximum number of evictions allowed per bucket-placement chain.
    /// Set to 0 to auto-select (10 * slots-per-part). Default is 0.
    /// </summary>
    public uint MaxEvictionsPerChain { get; set; }

    /// <summary>
    /// Number of recently evicted buckets excluded while searching for best collision candidates.
    /// Higher values reduce cycles but may reduce successful pilot choices. Default is 16.
    /// </summary>
    public int RecentEvictionWindow
    {
        get;
        set
        {
            Validator.RequireThat(value > 0);
            field = value;
        }
    } = 16;

    /// <summary>
    /// Randomize the starting pilot when searching best collision candidates.
    /// Usually helps avoid eviction cycles. Default is true.
    /// </summary>
    public bool RandomizePilotSearchStart { get; set; } = true;
}
