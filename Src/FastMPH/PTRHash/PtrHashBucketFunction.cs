namespace Genbox.FastMPH.PTRHash;

/// <summary>
/// Bucket mapping function used to skew bucket sizes.
/// </summary>
public enum PtrHashBucketFunction : byte
{
    /// <summary>
    /// Uniform bucket distribution. Fastest bucket mapping.
    /// </summary>
    Linear = 0,

    /// <summary>
    /// Slightly skewed distribution based on x*x with epsilon blend.
    /// </summary>
    SquareEps = 1,

    /// <summary>
    /// More skewed distribution based on cubic transform with epsilon blend.
    /// </summary>
    CubicEps = 2
}
