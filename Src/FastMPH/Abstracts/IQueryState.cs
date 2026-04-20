using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>
/// Contains the details of a hash function
/// </summary>
[PublicAPI]
public interface IQueryState<in TKey> : IPackable where TKey : notnull
{
    /// <summary>
    /// Search the hash function for the specified key.
    /// </summary>
    /// <param name="key">The key to search for</param>
    /// <returns>The index of the key</returns>
    uint Search(TKey key);
}