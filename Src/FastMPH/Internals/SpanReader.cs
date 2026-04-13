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

    public ushort ReadUInt16()
    {
        ushort value = MemoryMarshal.Read<ushort>(_span);
        _span = _span[sizeof(ushort)..];
        return value;
    }

    public ulong ReadUInt64()
    {
        ulong value = MemoryMarshal.Read<ulong>(_span);
        _span = _span[sizeof(ulong)..];
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

    public uint[] ReadUInt32Array()
    {
        uint length = ReadUInt32();
        uint[] values = new uint[length];
        for (int i = 0; i < length; i++)
            values[i] = ReadUInt32();
        return values;
    }

    public byte[] ReadByteArray()
    {
        uint length = ReadUInt32();
        byte[] values = new byte[length];
        for (int i = 0; i < length; i++)
            values[i] = ReadByte();
        return values;
    }

    public ushort[] ReadUInt16Array()
    {
        uint length = ReadUInt32();
        ushort[] values = new ushort[length];
        for (int i = 0; i < length; i++)
            values[i] = ReadUInt16();
        return values;
    }

    public readonly int BytesRead() => _org.Length - _span.Length;
}