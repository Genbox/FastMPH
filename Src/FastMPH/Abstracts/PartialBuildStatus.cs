using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>Represents the result of a partial hash build.</summary>
[PublicAPI]
public enum PartialBuildStatus
{
    /// <summary>All keys were mapped successfully.</summary>
    Success,

    /// <summary>Some keys could not be mapped.</summary>
    Partial,

    /// <summary>Construction failed due to invalid inputs.</summary>
    Failure
}