using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
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
[SuppressMessage("Minor Code Smell", "S2325:Methods and properties that don\'t access instance data should be static")]
public partial class BmzBuilder<TKey> : IMinimalHashBuilder<TKey, BmzMinimalState<TKey>, BmzMinimalSettings> where TKey : notnull
{
    private const uint BufSize = 1024 * 64;

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out BmzMinimalState<TKey>? state, BmzMinimalSettings? settings = null)
    {
        settings ??= new BmzMinimalSettings();

        if (!TryCreateMinimalState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateMinimalCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateMinimalState(int numKeys, BmzMinimalSettings settings, [NotNullWhen(true)]out IBuildState? state)
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
    public bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, BmzMinimalSettings settings, [NotNullWhen(true)]out BmzMinimalState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        LogCreating(keys.Length, settings.Vertices);

        if (!GenerateEdges(build.Graph, build.NumVertices, seed, keys, hashFunc))
        {
            LogFailure();
            queryState = null;
            return false;
        }

        // Mapping step
        uint biggestGValue = 0;
        uint biggestEdgeValue = 1;

        Graph graph = build.Graph;

        // Ordering step
        LogStartOrdering();
        graph.ObtainCriticalNodes();

        // Searching step
        LogStartSearching();

        //Genbox: Originally lookupTable (g) was allocated on each loop. I've moved it out and reuse it.
        Array.Clear(build.LookupTable);
        Array.Clear(build.Visited);
        Array.Clear(build.UsedEdges);

        bool restartMapping = false;

        for (uint i = 0; i < build.NumVertices; ++i) // critical nodes
        {
            //Genbox: Inverted the if-statement to reduce nesting
            if (!graph.NodeIsCritical(i) || GetBit(build.Visited, i))
                continue;

            if (settings.Vertices > 1.14)
                restartMapping = TraverseCriticalNodes(graph, build.LookupTable, build.NumEdges, i, ref biggestGValue, ref biggestEdgeValue, build.UsedEdges, build.Visited);
            else
                restartMapping = TraverseCriticalNodesHeuristic(graph, build.LookupTable, build.NumEdges, i, ref biggestGValue, ref biggestEdgeValue, build.UsedEdges, build.Visited);

            if (restartMapping)
                break;
        }

        if (!restartMapping)
        {
            LogTraversingNonCriticalVertices();
            TraverseNonCriticalNodes(graph, build.LookupTable, build.NumEdges, build.NumVertices, build.UsedEdges, build.Visited);
        }
        else
        {
            LogFailure();
            queryState = null;
            return false;
        }

        uint[] lookupTable = GC.AllocateUninitializedArray<uint>(build.LookupTable.Length);
        Array.Copy(build.LookupTable, lookupTable, lookupTable.Length);
        queryState = new BmzMinimalState<TKey>(build.NumVertices, seed, lookupTable, hashFunc);
        LogSuccess();
        return true;
    }

    private bool GenerateEdges<T>(Graph graph, uint numVertices, ulong seed, ReadOnlySpan<T> keys, HashFunc<T> hashCode) where T : notnull
    {
        LogGeneratingEdges(numVertices);
        graph.ClearEdges();

        for (int i = 0; i < keys.Length; i++)
        {
            T key = keys[i];
            ulong h = hashCode(key, seed);
            uint h1 = (uint)h % numVertices;
            uint h2 = (uint)(h >> 32) % numVertices;

            if (h1 == h2 && ++h2 >= numVertices)
                h2 = 0;

            if (h1 == h2)
            {
                LogSelfLoop(h1, h2);
                return false;
            }

            //Genbox: inlined the boolean variable returned by ContainsEdge here
            if (graph.ContainsEdge(h1, h2))
            {
                LogNonSimpleGraph();
                return false; // checking multiple edge restriction.
            }

            LogAddingEdge(h1, h2);
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
            while ((u = graph.NextNeighbor(ref it)) != Graph.GraphNoNeighbor)
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

                    while ((lav = graph.NextNeighbor(ref it1)) != Graph.GraphNoNeighbor)
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
                while ((lav = graph.NextNeighbor(ref it1)) != Graph.GraphNoNeighbor)
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

            while ((u = graph.NextNeighbor(ref it)) != Graph.GraphNoNeighbor)
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
                    if (nextGIndex < numUnusedGValues)
                        nextG = unusedGValues[nextGIndex++];
                    else
                    {
                        nextG = biggestGValue + 1;
                        nextGIndex = uint.MaxValue;
                    }

                    it1 = graph.GetGraphIterator(u);
                    collision = false;

                    while ((lav = graph.NextNeighbor(ref it1)) != Graph.GraphNoNeighbor)
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
                            unusedGValuesCapacity += BufSize;
                            Array.Resize(ref unusedGValues, (int)unusedGValuesCapacity);
                        }
                        unusedGValues[numUnusedGValues++] = nextG;
                    }

                    if (nextG > biggestGValue)
                        biggestGValue = nextG;
                }

                nextGIndex--;

                if (nextGIndex < numUnusedGValues)
                    unusedGValues[nextGIndex] = unusedGValues[--numUnusedGValues];

                // Marking used edges...
                it1 = graph.GetGraphIterator(u);
                while ((lav = graph.NextNeighbor(ref it1)) != Graph.GraphNoNeighbor)
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

    private void Traverse(Graph graph, uint[] g, byte[] usedEdges, uint startVertex, ref uint unusedEdgeIndex, byte[] visited)
    {
        // Iterative DFS to avoid stack overflow on large inputs
        Stack<uint> stack = new Stack<uint>();
        stack.Push(startVertex);

        while (stack.Count > 0)
        {
            uint v = stack.Pop();
            GraphIterator it = graph.GetGraphIterator(v);

            uint neighbor;
            while ((neighbor = graph.NextNeighbor(ref it)) != Graph.GraphNoNeighbor)
            {
                if (GetBit(visited, neighbor))
                    continue;

                LogVisitingNeighbor(neighbor);

                unusedEdgeIndex = NextUnusedEdge(usedEdges, unusedEdgeIndex);
                g[neighbor] = unusedEdgeIndex - g[v];

                SetBit(visited, neighbor);
                unusedEdgeIndex++;

                stack.Push(neighbor);
            }
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

    private sealed class BuildState : IBuildState
    {
        public Graph Graph = null!;
        public uint[] LookupTable = [];
        public byte[] Visited = [];
        public byte[] UsedEdges = [];
        public uint NumEdges;
        public uint NumVertices;

        public static bool TryCreate(int keysLength, BmzMinimalSettings settings, Microsoft.Extensions.Logging.ILogger logger, [NotNullWhen(true)]out BuildState? state)
        {
            uint numEdges = (uint)keysLength;
            uint numVertices = (uint)Math.Ceiling(settings.Vertices * numEdges);

            if (numVertices < 5)
                numVertices = 5;

            state = new BuildState
            {
                NumEdges = numEdges,
                NumVertices = numVertices,
                Graph = new Graph(logger, numVertices, numEdges),
                LookupTable = GC.AllocateUninitializedArray<uint>((int)numVertices),
                Visited = GC.AllocateUninitializedArray<byte>(((int)numVertices / 8) + 1),
                UsedEdges = GC.AllocateUninitializedArray<byte>(((int)numEdges / 8) + 1)
            };

            return true;
        }

        public void Reset()
        {
            Array.Clear(LookupTable, 0, LookupTable.Length);
            Array.Clear(Visited, 0, Visited.Length);
            Array.Clear(UsedEdges, 0, UsedEdges.Length);
        }
    }

    private sealed class Queue
    {
        private readonly uint _capacity;
        private readonly uint[] _values;
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