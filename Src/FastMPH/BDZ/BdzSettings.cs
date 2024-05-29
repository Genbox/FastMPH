using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BDZ;

/// <summary>Settings for the BDZ perfect hash function</summary>
[PublicAPI]
public class BdzSettings : HashSettings
{
    private double _loadFactor = 1.23;

    /// <summary>
    /// Determines the load factor. Lower values gives more compact functions. Should be a float between [1.23, 2.0]. Default is 1.23 (1/1.23 = 81.3%).
    /// </summary>
    public double LoadFactor
    {
        get => _loadFactor;
        set
        {
            Validator.RequireThat(value is >= 1.23 and <= 2.0);
            _loadFactor = value;
        }
    }
}