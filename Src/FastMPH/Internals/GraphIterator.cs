namespace Genbox.FastMPH.Internals;

//TODO: Could be struct
internal sealed class GraphIterator(uint vertex, uint edge)
{
    public uint Vertex { get; } = vertex;
    public uint Edge { get; set; } = edge;
}