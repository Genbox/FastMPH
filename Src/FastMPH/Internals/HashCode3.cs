namespace Genbox.FastMPH.Internals;

internal delegate void HashCode3<in TKey>(TKey key, ulong seed, Span<uint> hashes);
internal delegate ulong HashCode<in TKey>(TKey key, ulong seed);