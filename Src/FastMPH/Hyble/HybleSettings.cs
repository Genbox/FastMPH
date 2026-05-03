using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.Hyble;

/// <summary>
/// Settings for the Hyble perfect hash function.
/// Hyble assumes full-avalanche key hashes and uses a 32-bit seeded hash pipeline.
/// </summary>
[PublicAPI]
public sealed class HybleSettings : HashSettings
{
    /// <summary>
    /// Expected number of keys per bucket. Higher values reduce bucket count but can make placement harder.
    /// Default is 5.
    /// </summary>
    public uint KeysPerBucket
    {
        get;
        set
        {
            Validator.RequireThat(value > 0);
            field = value;
        }
    } = 5;

    /// <summary>
    /// Step size used while scanning displacement candidates.
    /// Must be between 1 and 57. Only the bottom 57 bits of the 64-bit displacement mask are reliable.
    /// Default is 1.
    /// </summary>
    public uint DisplacementSearchStride
    {
        get;
        set
        {
            Validator.RequireThat(value is >= 1 and <= 57);
            field = value;
        }
    } = 57;
}