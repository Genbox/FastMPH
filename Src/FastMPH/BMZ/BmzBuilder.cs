using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.BDZ;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Compat;
using JetBrains.Annotations;
using static Genbox.FastMPH.Internals.BitArray;

namespace Genbox.FastMPH.BMZ;

/// <summary>
/// The BMZ algorithm is designed by Fabiano C. Botelho, David Menoti and Nivio Ziviani. It is based on cyclic random graphs.
/// Properties:
/// <list type="bullet">
///     <item>It constructs MPHFs in linear time.</item>
///     <item>It is not order preserving.</item>
///     <item>Items take up approximately 4cn bytes, where c is in the range [0.93,1.15].</item>
/// </list>
/// </summary>
[PublicAPI]
public partial class BmzBuilder<TKey> : IMinimalHashBuilder<TKey, BmzMinimalState<TKey>, BmzMinimalSettings> where TKey : notnull
{
    private const uint BufSize = 1024 * 64;

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, [NotNullWhen(true)]out BmzMinimalState<TKey>? state, BmzMinimalSettings? settings = null, IEqualityComparer<TKey>? comparer = null)
    {
        settings ??= new BmzMinimalSettings();
        comparer ??= EqualityComparer<TKey>.Default;

        HashCode<TKey> hashCode = HashHelper.GetHashFunc(comparer);

        LogCreating(keys.Length, settings.Vertices);

        uint numEdges = (uint)keys.Length;
        uint numVertices = (uint)Math.Ceiling(settings.Vertices * numEdges);

        if (numVertices < 5) // workaround for small key sets
            numVertices = 5;

        //Genbox: The original implementation created space for 3 seeds, but only ever used two.
        uint seed0 = 0;
        uint seed1 = 0;

        //TODO: check the paper for map iterations. I suspect we might be doing something wrong here.

        //Genbox: This was hardcoded to 20. I've moved it into settings and have it default to 20.
        uint iterationsMap = settings.MappingIterations;

        //Genbox: Originally lookupTable (g) was allocated on each loop. I've moved it out and reuse it.
        uint[] lookupTable = new uint[numVertices];

        Graph graph = new Graph(_logger, numVertices, numEdges);
        bool restartMapping = false;

        do
        {
            // Mapping step
            uint biggestGValue = 0;
            uint biggestEdgeValue = 1;

            //Genbox: Changed it so we can control the number of iterations from settings
            uint iterations = settings.Iterations;

            for (; iterations > 0; iterations--)
            {
                LogIteration(iterations);

                //Genbox: In the original code it called modulus to reduce the keyspace of the seed. However, I don't see why that is necessary.
                seed0 = (uint)RandomProvider.Random.Next();
                seed1 = (uint)RandomProvider.Random.Next();

                if (GenerateEdges(graph, numVertices, seed0, seed1, keys, hashCode))
                    break;
            }

            if (iterations == 0)
            {
                LogFailure();
                state = null;
                return false;
            }

            // Ordering step
            LogStartOrdering();
            graph.ObtainCriticalNodes();

            // Searching step
            LogStartSearching();

            byte[] visited = new byte[numVertices / 8 + 1];
            byte[] usedEdges = new byte[numEdges / 8 + 1];

            //Genbox: Originally lookupTable (g) was allocated on each loop. I've moved it out and reuse it.
            Array2.Clear(lookupTable);

            for (uint i = 0; i < numVertices; ++i) // critical nodes
            {
                //Genbox: Inverted the if-statement to reduce nesting
                if (!graph.NodeIsCritical(i) || GetBit(visited, i))
                    continue;

                //TODO: As settings is constant, we could get rid of the branching with a delegate
                if (settings.Vertices > 1.14)
                    restartMapping = TraverseCriticalNodes(graph, lookupTable, numEdges, i, ref biggestGValue, ref biggestEdgeValue, usedEdges, visited);
                else
                    restartMapping = TraverseCriticalNodesHeuristic(graph, lookupTable, numEdges, i, ref biggestGValue, ref biggestEdgeValue, usedEdges, visited);

                if (restartMapping)
                    break;
            }

            if (!restartMapping)
            {
                LogTraversingNonCriticalVertices();
                TraverseNonCriticalNodes(graph, lookupTable, numEdges, numVertices, usedEdges, visited);
            }
            else
            {
                iterationsMap--;
                LogRestartMappingStep(iterationsMap);
            }
        } while (restartMapping && iterationsMap > 0);

        if (iterationsMap == 0)
        {
            LogFailure();
            state = null;
            return false;
        }

        LogSuccess();
        state = new BmzMinimalState<TKey>(numVertices, seed0, seed1, lookupTable, hashCode);
        return true;
    }

    private bool GenerateEdges<T>(Graph graph, uint numVertices, uint seed0, uint seed1, ReadOnlySpan<T> keys, HashCode<T> hashCode) where T : notnull
    {
        LogGeneratingEdges(numVertices);
        graph.ClearEdges();

        for (int i = 0; i < keys.Length; i++)
        {
            T key = keys[i];
            uint h1 = hashCode(key, seed0) % numVertices;
            uint h2 = hashCode(key, seed1) % numVertices;

            if (h1 == h2 && ++h2 >= numVertices)
                h2 = 0;

            if (h1 == h2)
            {
                LogSelfLoop(key.ToString(), h1, h2);
                return false;
            }

            //Genbox: inlined the boolean variable returned by ContainsEdge here
            if (graph.ContainsEdge(h1, h2))
            {
                LogNonSimpleGraph();
                return false; // checking multiple edge restriction.
            }

            LogAddingEdge(key.ToString(), h1, h2);
            graph.AddEdge(h1, h2);
        }

        return true;
    }

    private static bool TraverseCriticalNodes(Graph graph, uint[] lookupTable, uint numEdges, uint v, ref uint biggestGValue, ref uint biggestEdgeValue, byte[] usedEdges, byte[] visited)
    {
        Queue queue = new Queue(graph.GetNumCriticalNodes() + 1);

        lookupTable[v] = (uint)Math.Ceiling(biggestEdgeValue / 2.0) - 1;
        SetBit(visited, v);

        uint nextG = (uint)Math.Floor(biggestEdgeValue / 2.0) /* next_g is incremented in the do..while statement*/;
        queue.Insert(v);

        while (!queue.IsEmpty())
        {
            v = queue.Remove();
            GraphIterator it = graph.GetGraphIterator(v);

            uint u; /* Auxiliary vertex */
            while ((u = graph.NextNeighbor(it)) != Graph.GraphNoNeighbor)
            {
                //Genbox: Inverted if-statement to reduce nesting
                if (!graph.NodeIsCritical(u) || GetBit(visited, u))
                    continue;

                bool collision = true;
                GraphIterator it1;
                uint lav; /* lookahead vertex */

                while (collision) // lookahead to resolve collisions
                {
                    nextG = biggestGValue + 1;
                    it1 = graph.GetGraphIterator(u);
                    collision = false;

                    while ((lav = graph.NextNeighbor(it1)) != Graph.GraphNoNeighbor)
                    {
                        //Genbox: Inverted if-statement to reduce nesting
                        if (!graph.NodeIsCritical(lav) || !GetBit(visited, lav))
                            continue;

                        if (nextG + lookupTable[lav] >= numEdges)
                            return true; // restart mapping step.

                        if (GetBit(usedEdges, nextG + lookupTable[lav]))
                        {
                            collision = true;
                            break;
                        }
                    }

                    if (nextG > biggestGValue)
                        biggestGValue = nextG;
                }

                // Marking used edges...
                it1 = graph.GetGraphIterator(u);
                while ((lav = graph.NextNeighbor(it1)) != Graph.GraphNoNeighbor)
                {
                    //Genbox: Inverted if-statement to reduce nesting
                    if (!graph.NodeIsCritical(lav) || !GetBit(visited, lav))
                        continue;

                    SetBit(usedEdges, nextG + lookupTable[lav]);

                    if (nextG + lookupTable[lav] > biggestEdgeValue)
                        biggestEdgeValue = nextG + lookupTable[lav];
                }

                lookupTable[u] = nextG; // Labelling vertex u.
                SetBit(visited, u);
                queue.Insert(u);
            }
        }
        return false;
    }

    private static bool TraverseCriticalNodesHeuristic(Graph graph, uint[] g, uint numEdges, uint v, ref uint biggestGValue, ref uint biggestEdgeValue, byte[] usedEdges, byte[] visited)
    {
        uint[] unusedGValues = null!;
        uint unusedGValuesCapacity = 0;
        uint numUnusedGValues = 0;

        Queue queue = new Queue((uint)(0.5 * graph.GetNumCriticalNodes()) + 1);

        g[v] = (uint)Math.Ceiling(biggestEdgeValue / 2.0) - 1;
        SetBit(visited, v);

        uint nextG = (uint)Math.Floor(biggestEdgeValue / 2.0) /* next_g is incremented in the do..while statement*/;
        queue.Insert(v);

        while (!queue.IsEmpty())
        {
            v = queue.Remove();
            GraphIterator it = graph.GetGraphIterator(v);
            uint u; /* Auxiliary vertex */

            while ((u = graph.NextNeighbor(it)) != Graph.GraphNoNeighbor)
            {
                //Genbox: Inverted if-statement to reduce nesting
                if (!graph.NodeIsCritical(u) || GetBit(visited, u))
                    continue;

                uint nextGIndex = 0;
                bool collision = true;
                GraphIterator it1;
                uint lav; /* lookahead vertex */

                while (collision) // lookahead to resolve collisions
                {
                    //TODO: Check this condition
                    if (nextGIndex < numUnusedGValues)
                        nextG = unusedGValues[nextGIndex++];
                    else
                    {
                        nextG = biggestGValue + 1;
                        nextGIndex = uint.MaxValue;
                    }

                    it1 = graph.GetGraphIterator(u);
                    collision = false;

                    while ((lav = graph.NextNeighbor(it1)) != Graph.GraphNoNeighbor)
                    {
                        //Genbox: Inverted if-statement to reduce nesting
                        if (!graph.NodeIsCritical(lav) || !GetBit(visited, lav))
                            continue;

                        if (nextG + g[lav] >= numEdges)
                            return true; // restart mapping step.

                        if (GetBit(usedEdges, nextG + g[lav]))
                        {
                            collision = true;
                            break;
                        }
                    }

                    if (collision && nextG > biggestGValue) // saving the current g value stored in next_g.
                    {
                        if (numUnusedGValues == unusedGValuesCapacity)
                        {
                            unusedGValues = new uint[unusedGValuesCapacity + BufSize];
                            unusedGValuesCapacity += BufSize;
                        }
                        unusedGValues[numUnusedGValues++] = nextG;
                    }

                    if (nextG > biggestGValue)
                        biggestGValue = nextG;
                }

                nextGIndex--;

                //TODO: Check this condition
                if (nextGIndex < numUnusedGValues)
                    unusedGValues[nextGIndex] = unusedGValues[--numUnusedGValues];

                // Marking used edges...
                it1 = graph.GetGraphIterator(u);
                while ((lav = graph.NextNeighbor(it1)) != Graph.GraphNoNeighbor)
                {
                    //Genbox: Inverted if-statement to reduce nesting
                    if (!graph.NodeIsCritical(lav) || !GetBit(visited, lav))
                        continue;

                    SetBit(usedEdges, nextG + g[lav]);

                    if (nextG + g[lav] > biggestEdgeValue)
                        biggestEdgeValue = nextG + g[lav];
                }
                g[u] = nextG; // Labelling vertex u.
                SetBit(visited, u);
                queue.Insert(u);
            }
        }
        return false;
    }

    private static uint NextUnusedEdge(byte[] usedEdges, uint unusedEdgeIndex)
    {
        //Genbox: simplified the while loop condition
        while (GetBit(usedEdges, unusedEdgeIndex))
            unusedEdgeIndex++;

        return unusedEdgeIndex;
    }

    private void Traverse(Graph graph, uint[] g, byte[] usedEdges, uint v, ref uint unusedEdgeIndex, byte[] visited)
    {
        GraphIterator it = graph.GetGraphIterator(v);

        uint neighbor;
        while ((neighbor = graph.NextNeighbor(it)) != Graph.GraphNoNeighbor)
        {
            if (GetBit(visited, neighbor))
                continue;

            LogVisitingNeighbor(neighbor);

            unusedEdgeIndex = NextUnusedEdge(usedEdges, unusedEdgeIndex);
            g[neighbor] = unusedEdgeIndex - g[v];

            SetBit(visited, neighbor);
            unusedEdgeIndex++;

            Traverse(graph, g, usedEdges, neighbor, ref unusedEdgeIndex, visited);
        }
    }

    private void TraverseNonCriticalNodes(Graph graph, uint[] g, uint numEdges, uint numVertices, byte[] usedEdges, byte[] visited)
    {
        uint unusedEdgeIndex = 0;

        for (uint i = 0; i < numEdges; i++)
        {
            uint v1 = graph.GetVertexId(i, 0);
            uint v2 = graph.GetVertexId(i, 1);

            if ((GetBit(visited, v1) && GetBit(visited, v2)) || (!GetBit(visited, v1) && !GetBit(visited, v2)))
                continue;

            if (GetBit(visited, v1))
                Traverse(graph, g, usedEdges, v1, ref unusedEdgeIndex, visited);
            else
                Traverse(graph, g, usedEdges, v2, ref unusedEdgeIndex, visited);
        }

        for (uint i = 0; i < numVertices; i++)
        {
            if (!GetBit(visited, i))
            {
                g[i] = 0;
                SetBit(visited, i);
                Traverse(graph, g, usedEdges, i, ref unusedEdgeIndex, visited);
            }
        }
    }

    //TODO: Could be struct
    private sealed class Queue
    {
        private readonly uint[] _values;
        private readonly uint _capacity;
        private uint _begin;
        private uint _end;

        public Queue(uint newCapacity)
        {
            uint capacityPlusOne = newCapacity + 1;
            _values = new uint[capacityPlusOne];
            _capacity = capacityPlusOne;
        }

        public bool IsEmpty() => _begin == _end;

        public void Insert(uint val)
        {
            Debug.Assert((_end + 1) % _capacity != _begin); // Is queue full?

            _end = (_end + 1) % _capacity;
            _values[_end] = val;
        }

        public uint Remove()
        {
            Debug.Assert(!IsEmpty()); // Is queue empty?

            _begin = (_begin + 1) % _capacity;
            return _values[_begin];
        }
    }
}