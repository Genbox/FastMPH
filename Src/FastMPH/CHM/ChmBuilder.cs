using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using static Genbox.FastMPH.Internals.BitArray;

namespace Genbox.FastMPH.CHM;

/// <summary>
/// The CHM algorithm is designed by Z.J. Czech, G. Havas, and B.S. Majewski.
/// Properties:
/// <list type="bullet">
///     <item>It constructs MPHFs in linear time.</item>
///     <item>It is not order preserving.</item>
///     <item>The resulting MPHFs can be stored using less than 8.0 bits per key.</item>
/// </list>
/// </summary>
[PublicAPI]
public sealed partial class ChmBuilder<TKey> : IMinimalHashBuilder<TKey, ChmMinimalState<TKey>, ChmMinimalSettings> where TKey : notnull
{
    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, Func<TKey, ulong> hashFunc, ulong seed, [NotNullWhen(true)] out ChmMinimalState<TKey>? state, ChmMinimalSettings? settings = null)
    {
        settings ??= new ChmMinimalSettings();

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(hashFunc);

        ChmBuildState buildState = new ChmBuildState();

        return TryCreateMinimalCore(keys, hashCode, settings, seed, buildState, out state);
    }

    private bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashCode<TKey> hashCode, ChmMinimalSettings settings, ulong seed, ChmBuildState buildState, [NotNullWhen(true)]out ChmMinimalState<TKey>? state)
    {
        LogCreating(keys.Length, settings.LoadFactor);

        uint numEdges = (uint)keys.Length;
        uint numVertices = (uint)Math.Ceiling(settings.LoadFactor * numEdges);

        buildState.EnsureForGraph(_logger, numVertices, numEdges);
        Graph graph = buildState.Graph;

        //Mapping step
        LogMappingStep();

        if (!GenerateEdges(graph, seed, numVertices, keys, hashCode))
        {
            LogFailed();
            state = null;
            return false;
        }

        //Assignment step
        LogAssignmentStep();

        buildState.EnsureForVertices((int)numVertices);
        byte[] visited = buildState.Visited;
        uint[] lookupTable = buildState.LookupTable;
        Array.Clear(visited, 0, visited.Length);
        Array.Clear(lookupTable, 0, lookupTable.Length);

        for (uint i = 0; i < numVertices; ++i)
        {
            if (!GetBit(visited, i))
            {
                lookupTable[i] = 0;
                Traverse(graph, lookupTable, visited, i);
            }
        }

        LogSuccess();
        state = new ChmMinimalState<TKey>(numVertices, numEdges, lookupTable, seed, hashCode);
        return true;
    }

    private sealed class ChmBuildState
    {
        public Graph Graph = null!;
        private uint _graphVertices;
        private uint _graphEdges;
        public byte[] Visited = [];
        public uint[] LookupTable = [];

        public void EnsureForGraph(ILogger logger, uint numVertices, uint numEdges)
        {
            if (Graph == null || _graphVertices != numVertices || _graphEdges != numEdges)
            {
                Graph = new Graph(logger, numVertices, numEdges);
                _graphVertices = numVertices;
                _graphEdges = numEdges;
            }
        }

        public void EnsureForVertices(int numVertices)
        {
            int visitedLength = (numVertices / 8) + 1;
            ArrayEnsure.EnsureCapacity(ref Visited, visitedLength);
            ArrayEnsure.EnsureCapacity(ref LookupTable, numVertices);
        }
    }

    private void Traverse(Graph graph, uint[] lookupTable, byte[] visited, uint v)
    {
        GraphIterator it = graph.GetGraphIterator(v);
        SetBit(visited, v);

        LogVisitingVertex(v);

        uint neighbor;
        bool isTraceEnabled = _logger.IsEnabled(LogLevel.Trace);
        while ((neighbor = graph.NextNeighbor(it)) != Graph.GraphNoNeighbor)
        {
            LogVisitingNeighbor(neighbor);

            if (GetBit(visited, neighbor))
                continue;

            uint edgeId = graph.GetEdgeId(v, neighbor);
            LogVisitingEdge(v, neighbor, edgeId);

            lookupTable[neighbor] = edgeId - lookupTable[v];

            if (isTraceEnabled)
                LogStatus(lookupTable[neighbor], edgeId, lookupTable[v]);

            Traverse(graph, lookupTable, visited, neighbor);
        }
    }

    private bool GenerateEdges<T>(Graph graph, ulong seed, uint numVertices, ReadOnlySpan<T> keys, HashCode<T> hashCode) where T : notnull
    {
        graph.ClearEdges();

        for (int e = 0; e < keys.Length; ++e)
        {
            T key = keys[e];
            ulong h = hashCode(key, seed);
            uint h1 = (uint)h % numVertices;
            uint h2 = (uint)(h >> 32) % numVertices;

            if (h1 == h2 && ++h2 >= numVertices)
                h2 = 0;

            if (h1 == h2)
            {
                LogSelfLoop(e);
                return false;
            }

            LogAddingEdge(h1, h2);
            graph.AddEdge(h1, h2);
        }

        if (graph.IsCyclic())
        {
            LogCyclicGraph();
            return false;
        }

        return true;
    }
}