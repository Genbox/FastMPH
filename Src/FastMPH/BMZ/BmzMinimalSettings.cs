using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BMZ;

/// <summary>Settings for the BMZ minimal perfect hash function</summary>
[PublicAPI]
public class BmzMinimalSettings : HashSettings
{
    /// <summary>
    /// The number of vertices to use for the graph. More vertices means a larger function. It must be in the range 0.93 and 1.15. Default is 1.15
    /// </summary>
    public double Vertices
    {
        get;
        set
        {
            Validator.RequireThat(value is >= 0.93 and <= 1.15);
            field = value;
        }
    } = 1.15;
}