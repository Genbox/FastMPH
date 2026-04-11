using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BBHash;

/// <summary>Settings for the BBHash minimal perfect hash function.</summary>
[PublicAPI]
public sealed class BbHashMinimalSettings : HashSettings
{
    /// <summary>
    /// First seed.
    /// Default is 0xAAAAAAAA55555555.
    /// </summary>
    public uint Seed0 { get; set; } = 0xAAAAAAAAU;

    /// <summary>
    /// Second seed.
    /// Default is 0x33333333CCCCCCCC.
    /// </summary>
    public uint Seed1 { get; set; } = 0x33333333U;

    /// <summary>
    /// Controls the domain size per level. Higher values usually improve build reliability at the cost of larger state.
    /// Default is 2.0.
    /// </summary>
    public double Gamma
    {
        get;
        set
        {
            Validator.RequireThat(value >= 1.0);
            field = value;
        }
    } = 2.0;

    /// <summary>
    /// Maximum number of levels in the collision cascade. Default is 25.
    /// </summary>
    public uint MaxLevels { get; set; } = 25;
}