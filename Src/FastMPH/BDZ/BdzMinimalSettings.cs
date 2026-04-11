using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BDZ;

/// <summary>Settings for the BDZ minimal perfect hash function</summary>
[PublicAPI]
public sealed class BdzMinimalSettings : BdzSettings
{
    /// <summary>
    /// The size of the precomputed rank information. A larger value means more compact functions, but slower evaluation time. Should be an integer between [3,10]. Default is 7.
    /// </summary>
    public byte NumBitsOfKey
    {
        get;
        set
        {
            Validator.RequireThat(value is >= 3 and <= 10);
            field = value;
        }
    } = 7;
}