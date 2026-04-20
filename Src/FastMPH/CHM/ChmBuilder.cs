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
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out ChmMinimalState<TKey>? state, ChmMinimalSettings? settings = null)
    {
        settings ??= new ChmMinimalSettings();

        if (!TryCreateMinimalState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateMinimalCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateMinimalState(int numKeys, ChmMinimalSettings settings, [NotNullWhen(true)]out IBuildState? state)
    {
        if (!BuildState.TryCreate(numKeys, settings, _logger, out BuildState? typed))
        {
            state = null;
            return false;
        }

        state = typed;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, ChmMinimalSettings settings, [NotNullWhen(true)]out ChmMinimalState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        LogCreating(keys.Length, settings.LoadFactor);

        //Mapping step
        LogMappingStep();

        if (!GenerateEdges(build.Graph, seed, build.NumVertices, keys, hashFunc))
        {
            LogFailed();
            queryState = null;
            return false;
        }

        //Assignment step
        LogAssignmentStep();

        Array.Clear(build.Visited, 0, build.Visited.Length);
        Array.Clear(build.LookupTable, 0, build.LookupTable.Length);

        for (uint i = 0; i < build.NumVertices; ++i)
        {
            if (!GetBit(build.Visited, i))
            {
                build.LookupTable[i] = 0;
                Traverse(build.Graph, build.LookupTable, build.Visited, i);
            }
        }

        uint[] lookupTable = GC.AllocateUninitializedArray<uint>(build.LookupTable.Length);
        Array.Copy(build.LookupTable, lookupTable, lookupTable.Length);
        queryState = new ChmMinimalState<TKey>(build.NumVertices, build.NumEdges, lookupTable, seed, hashFunc);

        LogSuccess();
        return true;
    }

    private void Traverse(Graph graph, uint[] lookupTable, byte[] visited, uint v)
    {
        GraphIterator it = graph.GetGraphIterator(v);
        SetBit(visited, v);

        LogVisitingVertex(v);

        uint neighbor;
        bool isTraceEnabled = _logger.IsEnabled(LogLevel.Trace);
        while ((neighbor = graph.NextNeighbor(ref it)) != Graph.GraphNoNeighbor)
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

    private bool GenerateEdges<T>(Graph graph, ulong seed, uint numVertices, ReadOnlySpan<T> keys, HashFunc<T> hashCode) where T : notnull
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

    private sealed class BuildState : IBuildState
    {
        public Graph Graph = null!;
        public byte[] Visited = [];
        public uint[] LookupTable = [];
        public uint NumVertices;
        public uint NumEdges;

        public static bool TryCreate(int keyLength, ChmMinimalSettings settings, ILogger logger, [NotNullWhen(true)]out BuildState? state)
        {
            uint numEdges = (uint)keyLength;
            uint numVertices = (uint)Math.Ceiling(settings.LoadFactor * numEdges);
            Graph graph = new Graph(logger, numVertices, numEdges);

            int visitedLength = ((int)numVertices / 8) + 1;

            state = new BuildState
            {
                Graph = graph,
                NumEdges = numEdges,
                NumVertices = numVertices,
                Visited = GC.AllocateUninitializedArray<byte>(visitedLength),
                LookupTable = GC.AllocateUninitializedArray<uint>((int)numVertices)
            };

            return true;
        }

        public void Reset()
        {
            Array.Clear(Visited, 0, Visited.Length);
            Array.Clear(LookupTable, 0, LookupTable.Length);
        }
    }
}