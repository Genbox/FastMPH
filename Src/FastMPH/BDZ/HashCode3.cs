namespace Genbox.FastMPH.BDZ;

public delegate void HashCode3<in TKey>(TKey key, uint seed, Span<uint> hashes);
public delegate uint HashCode<in TKey>(TKey key, uint seed);
