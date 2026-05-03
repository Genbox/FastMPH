using System.Buffers.Binary;

namespace Genbox.FastMPH.Internals.Helpers;

internal static class RandomHelper
{
    [ThreadStatic]
    private static Random? _rng;

    private static Random GetRng() => _rng ??= new Random(42 + Environment.CurrentManagedThreadId);

    public static ulong Next64()
    {
        Span<byte> buf = stackalloc byte[sizeof(ulong)];
        GetRng().NextBytes(buf);
        return BinaryPrimitives.ReadUInt64LittleEndian(buf);
    }
}