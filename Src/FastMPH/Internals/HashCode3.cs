namespace Genbox.FastMPH.Internals;

internal delegate void HashCode3<in TKey>(TKey key, uint seed, Span<uint> hashes);
internal delegate uint HashCode<in TKey>(TKey key, uint seed);