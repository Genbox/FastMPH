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

    public void WriteUInt16(ushort value)
    {
        MemoryMarshal.Write(_span, ref value);
        _span = _span[sizeof(ushort)..];
    }

    public void WriteUInt64(ulong value)
    {
        MemoryMarshal.Write(_span, ref value);
        _span = _span[sizeof(ulong)..];
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

    public void WriteByteArray(byte[] values)
    {
        WriteUInt32((uint)values.Length);
        foreach (byte value in values)
            WriteByte(value);
    }

    public void WriteUInt16Array(ushort[] values)
    {
        WriteUInt32((uint)values.Length);
        foreach (ushort value in values)
            WriteUInt16(value);
    }

    public void WriteUInt32Array(uint[] values)
    {
        WriteUInt32((uint)values.Length);
        foreach (uint value in values)
            WriteUInt32(value);
    }

    public readonly int BytesWritten() => _org.Length - _span.Length;
}