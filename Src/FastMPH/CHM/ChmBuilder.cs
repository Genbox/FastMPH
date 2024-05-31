using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;
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
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out ChmMinimalState<TKey>? state, ChmMinimalSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new ChmMinimalSettings();
        comparer ??= EqualityComparer<TKey>.Default;

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(comparer);

        LogCreating(keys.Length, settings.LoadFactor);

        uint numEdges = (uint)keys.Length;
        uint numVertices = (uint)Math.Ceiling(settings.LoadFactor * numEdges);

        Graph graph = new Graph(_logger, numVertices, numEdges);

        //Mapping step
        LogMappingStep();

        uint seed0 = 0;
        uint seed1 = 0;

        //Genbox: Made the number of iterations configurable
        uint iterations = settings.Iterations;

        //Genbox: Rewrote the iterations code here
        for (; iterations > 0; iterations--)
        {
            LogIteration(iterations);

            //Genbox: Don't use mod to reduce the keyspace. There is no need for it.
            seed0 = RandomHelper.Next();
            seed1 = RandomHelper.Next();

            if (GenerateEdges(graph, seed0, seed1, numVertices, keys, hashCode))
                break;
        }

        if (iterations == 0)
        {
            LogFailed();
            state = null;
            return false;
        }

        //Assignment step
        LogAssignmentStep();

        byte[] visited = new byte[numVertices / 8 + 1];
        uint[] lookupTable = new uint[numVertices];

        for (uint i = 0; i < numVertices; ++i)
        {
            if (!GetBit(visited, i))
            {
                lookupTable[i] = 0;
                Traverse(graph, lookupTable, visited, i);
            }
        }

        LogSuccess();
        state = new ChmMinimalState<TKey>(numVertices, numEdges, lookupTable, seed0, seed1, hashCode);
        return true;
    }

    private void Traverse(Graph graph, uint[] lookupTable, byte[] visited, uint v)
    {
        GraphIterator it = graph.GetGraphIterator(v);
        SetBit(visited, v);

        LogVisitingVertex(v);

        uint neighbor;
        while ((neighbor = graph.NextNeighbor(it)) != Graph.GraphNoNeighbor)
        {
            LogVisitingNeighbor(neighbor);

            if (GetBit(visited, neighbor))
                continue;

            LogVisitingEdge(v, neighbor, graph.GetEdgeId(v, neighbor));

            lookupTable[neighbor] = graph.GetEdgeId(v, neighbor) - lookupTable[v];

            LogStatus(lookupTable[neighbor], graph.GetEdgeId(v, neighbor), lookupTable[v]);

            Traverse(graph, lookupTable, visited, neighbor);
        }
    }

    private bool GenerateEdges<T>(Graph graph, uint seed0, uint seed1, uint numVertices, ReadOnlySpan<T> keys, HashCode<T> hashCode) where T : notnull
    {
        graph.ClearEdges();

        for (int e = 0; e < keys.Length; ++e)
        {
            T key = keys[e];

            uint h1 = hashCode(key, seed0) % numVertices;
            uint h2 = hashCode(key, seed1) % numVertices;

            if (h1 == h2 && ++h2 >= numVertices)
                h2 = 0;

            if (h1 == h2)
            {
                LogSelfLoop(e);
                return false;
            }

            LogAddingEdge(key.ToString(), h1, h2);

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