using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>
/// A hash builder used to construct a hash function
/// </summary>
/// <typeparam name="TKey">The type key</typeparam>
/// <typeparam name="TState">The type of state</typeparam>
/// <typeparam name="TSettings">The type of settings</typeparam>
[PublicAPI]
public interface IHashBuilder<TKey, TState, in TSettings> where TKey : notnull where TState : IHashState<TKey> where TSettings : HashSettings
{
    /// <summary>
    /// Create a perfect hash function.
    /// </summary>
    /// <param name="keys">The keys you want to generate the hash function for.</param>
    /// <param name="hashFunc">The hash function for keys.</param>
    /// <param name="state">Once successful this variable contains the finished function.</param>
    /// <param name="settings">Settings for this hash function</param>
    /// <returns>True if the function succeeded in creating a PHF</returns>
    bool TryCreate(ReadOnlySpan<TKey> keys, Func<TKey, uint> hashFunc, [NotNullWhen(true)]out TState? state, TSettings? settings = null);
}