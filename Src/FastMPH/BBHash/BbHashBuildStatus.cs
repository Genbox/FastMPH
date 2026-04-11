using JetBrains.Annotations;

namespace Genbox.FastMPH.BBHash;

/// <summary>Represents the result of a BBHash build.</summary>
[PublicAPI]
public enum BbHashBuildStatus
{
    /// <summary>All keys were mapped successfully.</summary>
    Success,

    /// <summary>Some keys could not be mapped.</summary>
    Partial,

    /// <summary>Construction failed due to invalid inputs.</summary>
    Failure
}