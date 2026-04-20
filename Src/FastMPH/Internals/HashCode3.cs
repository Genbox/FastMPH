namespace Genbox.FastMPH.Internals;

internal delegate void HashCode3<in TKey>(TKey key, ulong seed, Span<uint> hashes);
public delegate ulong HashFunc<in TKey>(TKey key, ulong seed);