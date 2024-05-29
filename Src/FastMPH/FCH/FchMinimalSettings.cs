using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.FCH;

/// <summary>Settings for the FCH minimal perfect hash function</summary>
[PublicAPI]
public sealed class FchMinimalSettings : HashSettings
{
    private double _bitsPerKey = 2.6;

    /// <summary>
    /// The number of bits per key. Must be 2 or more. Default is 2.6
    /// </summary>
    public double BitsPerKey
    {
        get => _bitsPerKey;
        set
        {
            Validator.RequireThat(value > 2.0);
            _bitsPerKey = value;
        }
    }
}