using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH.BMZ;

/// <summary>Settings for the BMZ minimal perfect hash function</summary>
[PublicAPI]
public class BmzMinimalSettings : HashSettings
{
    private double _vertices = 1.15;

    /// <summary>Controls the number of mapping iterations to attempt</summary>
    public uint MappingIterations { get; set; } = 20;

    /// <summary>
    /// The number of vertices to use for the graph. More vertices means a larger function. It must be in the range 0.93 and 1.15. Default is 1.15
    /// </summary>
    public double Vertices
    {
        get => _vertices;
        set
        {
            Validator.RequireThat(value is >= 0.93 and <= 1.15);
            _vertices = value;
        }
    }
}