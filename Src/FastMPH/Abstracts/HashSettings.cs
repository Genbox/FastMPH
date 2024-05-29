using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>Base class for settings</summary>
[PublicAPI]
public abstract class HashSettings
{
    /// <summary>
    /// The number of iterations to perform. A higher value means more attempts and therefore longer worst-case construction time.
    /// Defaults to 100
    /// </summary>
    public uint Iterations { get; set; } = 100;
}