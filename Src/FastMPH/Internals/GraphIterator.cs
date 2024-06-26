namespace Genbox.FastMPH.Internals;

internal sealed class GraphIterator(uint vertex, uint edge)
{
    public uint Vertex { get; } = vertex;
    public uint Edge { get; set; } = edge;
}