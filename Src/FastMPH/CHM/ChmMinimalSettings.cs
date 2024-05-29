using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHM;

/// <summary>Settings for the CHM minimal perfect hash function</summary>
[PublicAPI]
public sealed class ChmMinimalSettings : HashSettings
{
    private double _loadFactor = 2.09;

    /// <summary>
    /// The load factor. Must be a value larger than 2. Default is 2.09.
    /// </summary>
    public double LoadFactor
    {
        get => _loadFactor;
        set
        {
            Validator.RequireThat(value > 2.0);
            _loadFactor = value;
        }
    }
}