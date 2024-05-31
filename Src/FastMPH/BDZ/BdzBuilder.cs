using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Compat;
using JetBrains.Annotations;
using static Genbox.FastMPH.Internals.BitArray;

namespace Genbox.FastMPH.BDZ;

/// <summary>
/// The BDZ algorithm is designed by Fabiano C. Botelho, Djamal Belazzougui, Rasmus Pagh and Nivio Ziviani. It is based on acyclic random 3-graphs.
/// Properties:
/// <list type="bullet">
///     <item>It constructs both PHFs and MPHFs in linear time.</item>
///     <item>It is not order preserving.</item>
///     <item>Maximum load factor is 1/1.23 (81.3%) where items take up approximately 1.95 bits per key.</item>
/// </list>
/// </summary>
[PublicAPI]
public sealed partial class BdzBuilder<TKey> : IMinimalHashBuilder<TKey, BdzMinimalState<TKey>, BdzMinimalSettings>, IHashBuilder<TKey, BdzState<TKey>, BdzSettings> where TKey : notnull
{
    /// <inheritdoc />
    public bool TryCreate(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out BdzState<TKey>? state, BdzSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new BdzSettings();
        comparer ??= EqualityComparer<TKey>.Default;

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(comparer);

        LogCreating(keys.Length, settings.LoadFactor);

        if (!TryCreate(keys, hashCode, false, settings.LoadFactor, settings.Iterations, out uint numPartitions, out uint numVertices, out uint seed, out byte[]? lookupTable))
        {
            LogFailed();
            state = null;
            return false;
        }

        lookupTable = Optimize(lookupTable, numVertices);
        LogSuccess(seed, numPartitions);

        state = new BdzState<TKey>(numPartitions, lookupTable, seed, hashCode);
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out BdzMinimalState<TKey>? state, BdzMinimalSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new BdzMinimalSettings();
        comparer ??= EqualityComparer<TKey>.Default;

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(comparer);

        LogCreatingMinimal(keys.Length, settings.LoadFactor, settings.NumBitsOfKey);

        if (!TryCreate(keys, hashCode, true, settings.LoadFactor, settings.Iterations, out uint numPartitions, out uint numVertices, out uint seed, out byte[]? lookupTable))
        {
            LogFailed();
            state = null;
            return false;
        }

        uint indexInRank = 1U << settings.NumBitsOfKey;
        uint[] rankTable = RankingStep(lookupTable, indexInRank, (uint)Math.Ceiling(numVertices / (float)indexInRank));
        LogRankTable(string.Join(",", rankTable));
        LogSuccess(seed, numPartitions);

        state = new BdzMinimalState<TKey>(numPartitions, lookupTable, seed, settings.NumBitsOfKey, rankTable, hashCode);
        return true;
    }

    private bool TryCreate(ReadOnlySpan<TKey> keys, HashCode3<TKey> hashCode, bool minimal, double loadFactor, uint iterations, out uint numPartitions, out uint numVertices, out uint seed, [NotNullWhen(true)]out byte[]? lookupTable)
    {
        uint numEdges = (uint)keys.Length;
        numPartitions = (uint)Math.Ceiling(loadFactor * numEdges / 3);

        if (numPartitions % 2 == 0)
            numPartitions++;

        // workaround for small key sets
        if (numPartitions == 1)
            numPartitions = 3;

        numVertices = 3 * numPartitions;

        Graph graph = new Graph(numEdges, numVertices);
        uint[] queue = new uint[numEdges];

        LogCreatedHyperGraph(numEdges, numVertices);

        //Genbox: The original code seems to do 100 iterations but with the same 16 hash functions.
        //        I've implemented what I believe it should be instead.

        seed = 0;

        for (; iterations > 0; iterations--)
        {
            LogIteration(iterations);

            //Genbox: The original code used modulus to reduce the keyspace of the seed. However, I don't see any reason to do
            //        that, as the hash function works the same no matter the key space.
            seed = RandomHelper.Next();

            if (MappingStep(keys, seed, numPartitions, numEdges, graph, queue, hashCode))
                break;
        }

        if (iterations == 0)
        {
            LogFailed();
            lookupTable = null;
            return false;
        }

        lookupTable = AssigningStep(numVertices, graph, queue, minimal);
        LogLookupTable(string.Join(",", lookupTable));
        return true;
    }

    private static byte[] Optimize(byte[] lookupTable, uint numVertices)
    {
        uint newSize = (uint)Math.Ceiling(numVertices / 5.0f);
        byte[] newLookup = new byte[newSize];

        for (uint i = 0; i < numVertices; i++)
        {
            uint idx = i / 5;
            byte value = GetValue(lookupTable, i);
            newLookup[idx] = (byte)(newLookup[idx] + value * BdzShared.Pow3Table[i % 5]);
        }

        return newLookup;
    }

    private int GenerateQueue(uint numEdges, uint[] queue, Graph graph)
    {
        uint v0, v1, v2;
        uint queueHead = 0, queueTail = 0;
        byte[] markedEdge = new byte[(numEdges >> 3) + 1];

        for (uint i = 0; i < numEdges; i++)
        {
            v0 = graph.Edges[i].Vertices[0];
            v1 = graph.Edges[i].Vertices[1];
            v2 = graph.Edges[i].Vertices[2];

            if (graph.VertexDegree[v0] != 1 && graph.VertexDegree[v1] != 1 && graph.VertexDegree[v2] != 1)
                continue;

            if (GetBit(markedEdge, i))
                continue;

            queue[queueHead++] = i;
            SetBit(markedEdge, i);
        }

        LogQueueState(queueHead, queueTail);

        while (queueTail != queueHead)
        {
            uint currEdge = queue[queueTail++];
            LogRemovingEdge(currEdge);
            graph.RemoveEdge(currEdge);
            v0 = graph.Edges[currEdge].Vertices[0];
            v1 = graph.Edges[currEdge].Vertices[1];
            v2 = graph.Edges[currEdge].Vertices[2];

            uint tmpEdge;
            if (graph.VertexDegree[v0] == 1)
            {
                tmpEdge = graph.FirstEdge[v0];
                if (!GetBit(markedEdge, tmpEdge))
                {
                    queue[queueHead++] = tmpEdge;
                    SetBit(markedEdge, tmpEdge);
                }
            }

            if (graph.VertexDegree[v1] == 1)
            {
                tmpEdge = graph.FirstEdge[v1];
                if (!GetBit(markedEdge, tmpEdge))
                {
                    queue[queueHead++] = tmpEdge;
                    SetBit(markedEdge, tmpEdge);
                }
            }

            if (graph.VertexDegree[v2] != 1)
                continue;

            tmpEdge = graph.FirstEdge[v2];

            if (GetBit(markedEdge, tmpEdge))
                continue;

            queue[queueHead++] = tmpEdge;
            SetBit(markedEdge, tmpEdge);
        }

        return (int)(queueHead - numEdges); /* returns 0 if successful otherwise return negative number*/
    }

    private bool MappingStep(ReadOnlySpan<TKey> keys, uint seed, uint numPartitions, uint numEdges, Graph graph, uint[] queue, HashCode3<TKey> hashCode)
    {
        LogMappingStep(keys.Length, seed, numPartitions, numEdges);

        //Genbox: I've refactored the graph reset code into the graph itself for clarity
        graph.Clear();

        Span<uint> hashes = stackalloc uint[3];

        for (int i = 0; i < keys.Length; i++)
        {
            hashCode(keys[i], seed, hashes);
            hashes[0] = hashes[0] % numPartitions;
            hashes[1] = hashes[1] % numPartitions + numPartitions;
            hashes[2] = hashes[2] % numPartitions + (numPartitions << 1); //n + 2 * n

            LogAddingEdge(hashes[0], hashes[1], hashes[2]);
            graph.AddEdge(hashes[0], hashes[1], hashes[2]);
        }

        return GenerateQueue(numEdges, queue, graph) == 0;
    }

    private byte[] AssigningStep(uint numVertices, Graph graph, uint[] queue, bool minimal)
    {
        LogAssigningStep(queue.Length, numVertices);

        uint numEdges = graph.NumEdges;
        byte[] markedVertices = new byte[(numVertices >> 3) + 1];
        int sizeG = (int)Math.Ceiling(numVertices / 4.0);
        byte[] g = new byte[sizeG];

        if (minimal)
            Array2.Fill<byte>(g, 0xff, 0, sizeG);

        Array2.Fill<byte>(markedVertices, 0, 0, (int)((numVertices >> 3) + 1));

        for (uint i = numEdges - 1; i + 1 >= 1; i--)
        {
            uint currEdge = queue[i];
            uint v0 = graph.Edges[currEdge].Vertices[0];
            uint v1 = graph.Edges[currEdge].Vertices[1];
            uint v2 = graph.Edges[currEdge].Vertices[2];

            LogEntryB(v0, v1, v2, GetValue(g, v0), GetValue(g, v1), GetValue(g, v2), currEdge);

            if (!GetBit(markedVertices, v0))
            {
                if (!GetBit(markedVertices, v1))
                {
                    if (!minimal)
                        SetValue1(g, v1, BdzShared.Unassigned);

                    SetBit(markedVertices, v1);
                }
                if (!GetBit(markedVertices, v2))
                {
                    if (!minimal)
                        SetValue1(g, v2, BdzShared.Unassigned);

                    SetBit(markedVertices, v2);
                }

                if (minimal)
                    SetValue1(g, v0, (uint)((6 - (GetValue(g, v1) + GetValue(g, v2))) % 3));
                else
                    SetValue0(g, v0, (uint)((6 - (GetValue(g, v1) + GetValue(g, v2))) % 3));

                SetBit(markedVertices, v0);
            }
            else if (!GetBit(markedVertices, v1))
            {
                if (!GetBit(markedVertices, v2))
                {
                    if (!minimal)
                        SetValue1(g, v2, BdzShared.Unassigned);

                    SetBit(markedVertices, v2);
                }

                if (minimal)
                    SetValue1(g, v1, (uint)((7 - (GetValue(g, v0) + GetValue(g, v2))) % 3));
                else
                    SetValue0(g, v1, (uint)((7 - (GetValue(g, v0) + GetValue(g, v2))) % 3));

                SetBit(markedVertices, v1);
            }
            else
            {
                if (minimal)
                    SetValue1(g, v2, (uint)((8 - (GetValue(g, v0) + GetValue(g, v1))) % 3));
                else
                    SetValue0(g, v2, (uint)((8 - (GetValue(g, v0) + GetValue(g, v1))) % 3));

                SetBit(markedVertices, v2);
            }

            LogEntryA(v0, v1, v2, GetValue(g, v0), GetValue(g, v1), GetValue(g, v2));
        }

        return g;
    }

    private uint[] RankingStep(byte[] lookupTable, uint indexInRank, uint rankTableLength)
    {
        LogRankingStep(lookupTable.Length, indexInRank, rankTableLength);

        uint offset = 0U, count = 0U, size = indexInRank >> 2, numBytesTotal = (uint)lookupTable.Length;
        uint[] rankTable = new uint[rankTableLength];

        //Genbox: Lazy load the lookup table
        byte[] table = BdzShared.LookupTable.Value;

        //Genbox: This was a while loop with a break condition. I've simplified it to a for-loop.
        for (uint i = 1; i != rankTableLength; i++)
        {
            uint numBytes = size < numBytesTotal ? size : numBytesTotal;

            for (uint j = 0; j < numBytes; j++)
                count += table[lookupTable[offset + j]];

            rankTable[i] = count;
            offset += numBytes;
            numBytesTotal -= size;
        }

        return rankTable;
    }

    private sealed class Edge
    {
        public readonly uint[] NextEdges = new uint[3];
        public readonly uint[] Vertices = new uint[3];
    }

    private sealed class Graph
    {
        public readonly Edge[] Edges;
        public readonly uint[] FirstEdge;
        public readonly byte[] VertexDegree;
        private const uint NullEdge = 0xffffffff;

        public uint NumEdges;

        public Graph(uint numEdges, uint numVertices)
        {
            Edges = new Edge[numEdges];
            VertexDegree = new byte[numVertices];
            FirstEdge = new uint[numVertices];
            Array2.Fill<uint>(FirstEdge, 0xff);
        }

        internal void AddEdge(uint v0, uint v1, uint v2)
        {
            Edge edge = new Edge();
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
            Array2.Fill<uint>(FirstEdge, 0xff);
            Array2.Fill<byte>(VertexDegree, 0);
            NumEdges = 0;
        }
    }
}