using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.FCH;

/// <summary>Settings for the FCH minimal perfect hash function</summary>
[PublicAPI]
public sealed class FchMinimalSettings : HashSettings
{
    /// <summary>
    /// Maximum number of collision-free searching attempts per build call.
    /// </summary>
    public uint MaxSearchingIterations
    {
        get;
        set
        {
            Validator.RequireThat(value > 0);
            field = value;
        }
    } = 10;

    /// <summary>
    /// Maximum number of consecutive seed candidates that fail the h2 pre-collision check.
    /// </summary>
    public uint MaxSeedGenerationIterations
    {
        get;
        set
        {
            Validator.RequireThat(value > 0);
            field = value;
        }
    } = 1000;

    /// <summary>
    /// The number of bits per key. Must be 2 or more. Default is 2.6
    /// </summary>
    public double BitsPerKey
    {
        get;
        set
        {
            Validator.RequireThat(value > 2.0);
            field = value;
        }
    } = 2.6;
}