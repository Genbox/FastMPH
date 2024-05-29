using System.Runtime.InteropServices;

namespace Genbox.FastMPH.Internals;

[StructLayout(LayoutKind.Auto)]
internal ref struct SpanReader(ReadOnlySpan<byte> span)
{
    private readonly ReadOnlySpan<byte> _org = span;
    private ReadOnlySpan<byte> _span = span;

    public uint ReadUInt32()
    {
        uint value = MemoryMarshal.Read<uint>(_span);
        _span = _span[sizeof(uint)..];
        return value;
    }

    public byte ReadByte()
    {
        byte value = _span[0];
        _span = _span[sizeof(byte)..];
        return value;
    }

    public double ReadDouble()
    {
        double value = MemoryMarshal.Read<double>(_span);
        _span = _span[sizeof(double)..];
        return value;
    }

    public readonly int BytesRead() => _org.Length - _span.Length;
}