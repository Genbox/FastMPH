using System.Runtime.InteropServices;

namespace Genbox.FastMPH.Internals;

[StructLayout(LayoutKind.Auto)]
internal ref struct SpanWriter(Span<byte> span)
{
    private Span<byte> _span = span;
    private readonly Span<byte> _org = span;

    public void WriteUInt32(uint value)
    {
        MemoryMarshal.Write(_span, ref value);
        _span = _span[sizeof(uint)..];
    }

    public void WriteByte(byte value)
    {
        _span[0] = value;
        _span = _span[sizeof(byte)..];
    }

    public void WriteDouble(double value)
    {
        MemoryMarshal.Write(_span, ref value);
        _span = _span[sizeof(double)..];
    }

    public readonly int BytesWritten() => _org.Length - _span.Length;
}