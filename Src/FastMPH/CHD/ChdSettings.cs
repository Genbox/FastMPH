using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHD;

/// <summary>Settings for the CHD perfect hash function</summary>
[PublicAPI]
public class ChdSettings : HashSettings
{
    public double LoadFactor
    {
        get;
        set
        {
            Validator.RequireThat(value is >= 0.5 and <= 0.99);
            field = value;
        }
    } = 0.5;

    /// <summary>
    /// Set to true to utilize heuristics
    /// </summary>
    public bool UseHeuristics { get; set; } = true;

    /// <summary>
    /// Maximum number of keys per bin. Used for T-perfect hash function which has at most T collisions. It should be between [1, 128]. Default is 1.
    /// </summary>
    public byte KeysPerBin
    {
        get;
        set
        {
            Validator.RequireThat(value is >= 1 and <= 128);
            field = value;
        }
    } = 1;

    /// <summary>
    /// Average number of keys per bucket. The larger value means slower construction.
    /// Value should be between [1, 32]. Default is 4.
    /// </summary>
    public byte KeysPerBucket
    {
        get;
        set
        {
            Validator.RequireThat(value is >= 1 and <= 32);
            field = value;
        }
    } = 4;
}