using JetBrains.Annotations;

namespace Genbox.FastMPH.Abstracts;

/// <summary>Interface for hash functions that can be packed using binary serialization</summary>
[PublicAPI]
public interface IPackable
{
    /// <summary>
    /// Get the number of bytes needed to store the data structure
    /// </summary>
    uint GetPackedSize();

    /// <summary>
    /// Serialize the hash function. Call <see cref="GetPackedSize" /> to get the size of the serialized data.
    /// </summary>
    /// <param name="buffer">A span that you provide. It must have enough space for the serialized data.</param>
    void Pack(Span<byte> buffer);
}