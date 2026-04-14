using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>Contains a partial hash build result.</summary>
[PublicAPI]
public sealed class PartialBuildResult<TKey, TState>(TState state, Dictionary<TKey, uint> remainder) where TKey : notnull where TState : IHashState<TKey>
{
    /// <summary>The constructed hash state.</summary>
    public TState State { get; } = state;

    /// <summary>Keys that were not mapped by <see cref="State"/>.</summary>
    public Dictionary<TKey, uint> Remainder { get; } = remainder;
}