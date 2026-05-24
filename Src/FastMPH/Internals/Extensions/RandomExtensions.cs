using System.Buffers.Binary;

namespace Genbox.FastMPH.Internals.Extensions;

internal static class RandomExtensions
{
    internal static ulong Next64(this Random random)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        random.NextBytes(buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }
}