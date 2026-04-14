using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>
/// A hash builder that can return partial construction results.
/// </summary>
/// <typeparam name="TKey">The type of key.</typeparam>
/// <typeparam name="TState">The type of state.</typeparam>
/// <typeparam name="TSettings">The type of settings.</typeparam>
[PublicAPI]
public interface IPartialHashBuilder<TKey, TState, in TSettings> where TKey : notnull where TState : IHashState<TKey> where TSettings : HashSettings
{
    /// <summary>
    /// Create a (possibly partial) hash function.
    /// </summary>
    /// <param name="keys">The keys you want to generate the hash function for.</param>
    /// <param name="hashFunc">The hash function for keys.</param>
    /// <param name="result">Contains the constructed state and any remaining keys. Null on failure.</param>
    /// <param name="settings">Settings for this hash function.</param>
    /// <returns>The build status.</returns>
    PartialBuildStatus CreatePartial(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, out PartialBuildResult<TKey, TState>? result, TSettings? settings = null);
}