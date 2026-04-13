using System.Runtime.InteropServices;

namespace Genbox.FastMPH.Internals;

[StructLayout(LayoutKind.Auto)]
internal struct GraphIterator(uint vertex, uint edge)
{
    public uint Vertex { get; } = vertex;
    public uint Edge { get; set; } = edge;
}