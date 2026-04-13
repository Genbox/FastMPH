using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Compat;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
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
    public bool TryCreate(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, [NotNullWhen(true)]out BdzState<TKey>? state, BdzSettings? settings = null)
    {
        settings ??= new BdzSettings();

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(hashFunc);
        BdzBuildState buildState = new BdzBuildState();

        return TryCreateCore(keys, hashCode, settings, seed, buildState, out state);
    }

    private bool TryCreateCore(ReadOnlySpan<TKey> keys, HashCode3<TKey> hashCode, BdzSettings settings, ulong seed, BdzBuildState buildState, [NotNullWhen(true)]out BdzState<TKey>? state)
    {
        LogCreating(keys.Length, settings.LoadFactor);

        if (!TryCreate(keys, hashCode, false, settings.LoadFactor, seed, buildState, out uint numPartitions, out uint numVertices, out byte[]? lookupTable))
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
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, [NotNullWhen(true)]out BdzMinimalState<TKey>? state, BdzMinimalSettings? settings = null)
    {
        settings ??= new BdzMinimalSettings();

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(hashFunc);
        BdzBuildState buildState = new BdzBuildState();

        return TryCreateMinimalCore(keys, hashCode, settings, seed, buildState, out state);
    }

    private bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashCode3<TKey> hashCode, BdzMinimalSettings settings, ulong seed, BdzBuildState buildState, [NotNullWhen(true)]out BdzMinimalState<TKey>? state)
    {
        LogCreatingMinimal(keys.Length, settings.LoadFactor, settings.NumBitsOfKey);

        if (!TryCreate(keys, hashCode, true, settings.LoadFactor, seed, buildState, out uint numPartitions, out uint numVertices, out byte[]? lookupTable))
        {
            LogFailed();
            state = null;
            return false;
        }

        uint indexInRank = 1U << settings.NumBitsOfKey;
        uint[] rankTable = RankingStep(lookupTable, indexInRank, (uint)Math.Ceiling(numVertices / (double)indexInRank));
        LogRankTable(rankTable);
        LogSuccess(seed, numPartitions);

        state = new BdzMinimalState<TKey>(numPartitions, lookupTable, seed, settings.NumBitsOfKey, rankTable, hashCode);
        return true;
    }

    private bool TryCreate(ReadOnlySpan<TKey> keys, HashCode3<TKey> hashCode, bool minimal, double loadFactor, ulong seed, BdzBuildState buildState, out uint numPartitions, out uint numVertices, [NotNullWhen(true)]out byte[]? lookupTable)
    {
        uint numEdges = (uint)keys.Length;
        numPartitions = (uint)Math.Ceiling((loadFactor * numEdges) / 3);

        if (numPartitions % 2 == 0)
            numPartitions++;

        // workaround for small key sets
        if (numPartitions == 1)
            numPartitions = 3;

        numVertices = 3 * numPartitions;

        buildState.EnsureForGraph(numEdges, numVertices);
        buildState.EnsureForEdges(numEdges);
        buildState.EnsureForVertices(numVertices);

        Graph graph = buildState.Graph;
        uint[] queue = buildState.Queue;

        LogCreatedHyperGraph(numEdges, numVertices);

        if (!MappingStep(keys, seed, numPartitions, numEdges, graph, queue, buildState.MarkedEdge, hashCode))
        {
            LogFailed();
            lookupTable = null;
            return false;
        }

        lookupTable = AssigningStep(numVertices, graph, queue, minimal, buildState.MarkedVertices, buildState.LookupTable);
        LogLookupTable(lookupTable);
        return true;
    }

    private static byte[] Optimize(byte[] lookupTable, uint numVertices)
    {
        uint newSize = (uint)Math.Ceiling(numVertices / 5.0);
        byte[] newLookup = new byte[newSize];

        for (uint i = 0; i < numVertices; i++)
        {
            uint idx = i / 5;
            byte value = GetValue(lookupTable, i);
            newLookup[idx] = (byte)(newLookup[idx] + (value * BdzShared.Pow3Table[i % 5]));
        }

        return newLookup;
    }

    private int GenerateQueue(uint numEdges, uint[] queue, byte[] markedEdge, Graph graph)
    {
        uint v0, v1, v2;
        uint queueHead = 0, queueTail = 0;
        Array.Clear(markedEdge, 0, (int)((numEdges >> 3) + 1));

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

        return (int)queueHead - (int)numEdges; /* returns 0 if successful otherwise return negative number*/
    }

    private bool MappingStep(ReadOnlySpan<TKey> keys, ulong seed, uint numPartitions, uint numEdges, Graph graph, uint[] queue, byte[] markedEdge, HashCode3<TKey> hashCode)
    {
        LogMappingStep(keys.Length, seed, numPartitions, numEdges);

        //Genbox: I've refactored the graph reset code into the graph itself for clarity
        graph.Clear();

        Span<uint> hashes = stackalloc uint[3];

        for (int i = 0; i < keys.Length; i++)
        {
            hashCode(keys[i], seed, hashes);
            hashes[0] = hashes[0] % numPartitions;
            hashes[1] = (hashes[1] % numPartitions) + numPartitions;
            hashes[2] = (hashes[2] % numPartitions) + (numPartitions << 1); //n + 2 * n

            LogAddingEdge(hashes[0], hashes[1], hashes[2]);
            graph.AddEdge(hashes[0], hashes[1], hashes[2]);
        }

        return GenerateQueue(numEdges, queue, markedEdge, graph) == 0;
    }

    private byte[] AssigningStep(uint numVertices, Graph graph, uint[] queue, bool minimal, byte[] markedVertices, byte[] lookupTable)
    {
        LogAssigningStep(queue.Length, numVertices);

        uint numEdges = graph.NumEdges;
        byte[] g = lookupTable;
        int lookupLength = (int)Math.Ceiling(numVertices / 4.0);

        if (minimal)
            Array.Fill<byte>(g, 0xff, 0, lookupLength);
        else
            Array.Clear(g, 0, lookupLength);

        Array.Fill<byte>(markedVertices, 0, 0, (int)((numVertices >> 3) + 1));
        bool isTraceEnabled = _logger.IsEnabled(LogLevel.Trace);

        for (uint i = numEdges - 1; i + 1 >= 1; i--)
        {
            uint currEdge = queue[i];
            uint v0 = graph.Edges[currEdge].Vertices[0];
            uint v1 = graph.Edges[currEdge].Vertices[1];
            uint v2 = graph.Edges[currEdge].Vertices[2];

            if (isTraceEnabled)
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

            if (isTraceEnabled)
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

    private sealed class BdzBuildState
    {
        public Graph? Graph;
        public uint[] Queue = [];
        public byte[] MarkedEdge = [];
        public byte[] MarkedVertices = [];
        public byte[] LookupTable = [];
        private uint _numEdges;
        private uint _numVertices;

        public void EnsureForGraph(uint numEdges, uint numVertices)
        {
            if (Graph == null || _numEdges != numEdges || _numVertices != numVertices)
            {
                Graph = new Graph(numEdges, numVertices);
                _numEdges = numEdges;
                _numVertices = numVertices;
            }
        }

        public void EnsureForEdges(uint numEdges)
        {
            ArrayEnsure.EnsureCapacity(ref Queue, (int)numEdges);

            int markedEdgeLength = (int)((numEdges >> 3) + 1);
            ArrayEnsure.EnsureCapacity(ref MarkedEdge, markedEdgeLength);
        }

        public void EnsureForVertices(uint numVertices)
        {
            int markedVerticesLength = (int)((numVertices >> 3) + 1);
            ArrayEnsure.EnsureCapacity(ref MarkedVertices, markedVerticesLength);

            int lookupLength = (int)Math.Ceiling(numVertices / 4.0);
            ArrayEnsure.EnsureCapacity(ref LookupTable, lookupLength);
        }
    }

    private sealed class Edge
    {
        public readonly uint[] NextEdges = new uint[3];
        public readonly uint[] Vertices = new uint[3];
    }

    private sealed class Graph
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
}