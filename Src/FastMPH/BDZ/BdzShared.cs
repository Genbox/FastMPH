using System.Diagnostics;

namespace Genbox.FastMPH.BDZ;

internal static class BdzShared
{
    internal const uint Unassigned = 3U;
    internal static readonly byte[] Pow3Table = [1, 3, 9, 27, 81];

    // table used for looking up the number of assigned vertices an 8-bit integer
    internal static readonly Lazy<byte[]> LookupTable = new Lazy<byte[]>(() =>
    [
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3, 2, 2, 2, 2, 1,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3, 2, 2, 2, 2, 1,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        4, 4, 4, 3, 4, 4, 4, 3, 4, 4, 4, 3, 3, 3, 3, 2,
        3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3, 2, 2, 2, 2, 1,
        3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3, 2, 2, 2, 2, 1,
        3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3, 2, 2, 2, 2, 1,
        3, 3, 3, 2, 3, 3, 3, 2, 3, 3, 3, 2, 2, 2, 2, 1,
        2, 2, 2, 1, 2, 2, 2, 1, 2, 2, 2, 1, 1, 1, 1, 0
    ]);

    //Genbox: This is static such that it is shared among all instances of the state
    internal static readonly Lazy<byte[][]> SearchTable = new Lazy<byte[][]>(() => new byte[][]
    {
        [0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0],
        [0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 0, 1],
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1],
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
    });
}

internal sealed class Edge
{
    public readonly uint[] NextEdges = new uint[3];
    public readonly uint[] Vertices = new uint[3];
}

internal sealed class Graph
{
    private const uint NullEdge = 0xffffffff;
    public readonly Edge[] Edges;
    public readonly uint[] FirstEdge;
    public readonly byte[] VertexDegree;

    public uint NumEdges;

    public Graph(uint numEdges, uint numVertices)
    {
        Edges = new Edge[numEdges];
        VertexDegree = new byte[numVertices];
        FirstEdge = new uint[numVertices];
        Array.Fill(FirstEdge, NullEdge);
    }

    internal void AddEdge(uint v0, uint v1, uint v2)
    {
        Edge edge = Edges[NumEdges] ?? new Edge();
        edge.Vertices[0] = v0;
        edge.Vertices[1] = v1;
        edge.Vertices[2] = v2;
        edge.NextEdges[0] = FirstEdge[v0];
        edge.NextEdges[1] = FirstEdge[v1];
        edge.NextEdges[2] = FirstEdge[v2];

        FirstEdge[v0] = FirstEdge[v1] = FirstEdge[v2] = NumEdges;
        VertexDegree[v0]++;
        VertexDegree[v1]++;
        VertexDegree[v2]++;

        Edges[NumEdges] = edge;
        NumEdges++;
    }

    public void RemoveEdge(uint currentEdge)
    {
        //Genbox: NumEdges is not decremented here. Possible bug?

        int j = 0;
        for (int i = 0; i < 3; i++)
        {
            uint vert = Edges[currentEdge].Vertices[i];
            uint edge1 = FirstEdge[vert];
            uint edge2 = NullEdge;

            while (edge1 != currentEdge && edge1 != NullEdge)
            {
                edge2 = edge1;

                if (Edges[edge1].Vertices[0] == vert)
                    j = 0;
                else if (Edges[edge1].Vertices[1] == vert)
                    j = 1;
                else
                    j = 2;

                edge1 = Edges[edge1].NextEdges[j];
            }

            Debug.Assert(edge1 != NullEdge);

            if (edge2 != NullEdge)
                Edges[edge2].NextEdges[j] = Edges[edge1].NextEdges[i];
            else
                FirstEdge[vert] = Edges[edge1].NextEdges[i];

            VertexDegree[vert]--;
        }
    }

    public void Clear()
    {
        Array.Fill(FirstEdge, NullEdge);
        Array.Fill<byte>(VertexDegree, 0);
        NumEdges = 0;
    }
}