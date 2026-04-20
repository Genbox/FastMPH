using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using Genbox.FastMPH.Internals.Helpers;
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
    public bool TryCreate(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out BdzState<TKey>? state, BdzSettings? settings = null)
    {
        settings ??= new BdzSettings();

        if (!TryCreateState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateState(int numKeys, BdzSettings settings, [NotNullWhen(true)]out IBuildState? state)
    {
        if (!BuildState.TryCreate(numKeys, settings.LoadFactor, out BuildState? typed))
        {
            state = null;
            return false;
        }

        state = typed;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, BdzSettings settings, [NotNullWhen(true)]out BdzState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        LogCreating(keys.Length, settings.LoadFactor);

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(hashFunc);

        if (!TryCreateCoreInternal(keys, hashCode, seed, false, build.NumEdges, build.NumVertices, build.NumPartitions, build.Graph, build.Queue, build.MarkedEdge, build.MarkedVertices, build.LookupTable))
        {
            LogFailed();
            queryState = null;
            return false;
        }

        byte[] optimized = Optimize(build.LookupTable, build.NumVertices);
        LogSuccess(seed, build.NumPartitions);

        queryState = new BdzState<TKey>(build.NumPartitions, optimized, seed, hashFunc);
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimal(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, [NotNullWhen(true)]out BdzMinimalState<TKey>? state, BdzMinimalSettings? settings = null)
    {
        settings ??= new BdzMinimalSettings();

        if (!TryCreateMinimalState(keys.Length, settings, out IBuildState? buildState))
        {
            state = null;
            return false;
        }

        ulong seed = RandomHelper.Next64();
        return TryCreateMinimalCore(keys, hashFunc, seed, buildState, settings, out state);
    }

    /// <inheritdoc />
    public bool TryCreateMinimalState(int numKeys, BdzMinimalSettings settings, [NotNullWhen(true)]out IBuildState? state)
    {
        if (!BuildState.TryCreate(numKeys, settings.LoadFactor, out BuildState? typed))
        {
            state = null;
            return false;
        }

        state = typed;
        return true;
    }

    /// <inheritdoc />
    public bool TryCreateMinimalCore(ReadOnlySpan<TKey> keys, HashFunc<TKey> hashFunc, ulong seed, IBuildState state, BdzMinimalSettings settings, [NotNullWhen(true)]out BdzMinimalState<TKey>? queryState)
    {
        if (state is not BuildState build)
            throw new ArgumentException("Invalid build state type", nameof(state));

        LogCreatingMinimal(keys.Length, settings.LoadFactor, settings.NumBitsOfKey);

        HashCode3<TKey> hashCode = HashHelper.GetHashFunc3(hashFunc);

        if (!TryCreateCoreInternal(keys, hashCode, seed, true, build.NumEdges, build.NumVertices, build.NumPartitions, build.Graph, build.Queue, build.MarkedEdge, build.MarkedVertices, build.LookupTable))
        {
            LogFailed();
            queryState = null;
            return false;
        }

        uint indexInRank = 1U << settings.NumBitsOfKey;
        uint[] rankTable = RankingStep(build.LookupTable, indexInRank, (uint)Math.Ceiling(build.NumVertices / (double)indexInRank));
        LogRankTable(rankTable);
        LogSuccess(seed, build.NumPartitions);

        byte[] lookupTable = GC.AllocateUninitializedArray<byte>(build.LookupTable.Length);
        Array.Copy(build.LookupTable, lookupTable, lookupTable.Length);
        queryState = new BdzMinimalState<TKey>(seed, build.NumPartitions, settings.NumBitsOfKey, lookupTable, rankTable, hashFunc);
        return true;
    }

    private bool TryCreateCoreInternal(ReadOnlySpan<TKey> keys, HashCode3<TKey> hashCode, ulong seed, bool minimal, uint numEdges, uint numVertices, uint numPartitions, Graph graph, uint[] queue, byte[] markedEdge, byte[] markedVertices, byte[] lookupTable)
    {
        LogCreatedHyperGraph(numEdges, numVertices);

        if (!MappingStep(keys, seed, numPartitions, numEdges, graph, queue, markedEdge, hashCode))
        {
            LogFailed();
            return false;
        }

        AssigningStep(numVertices, graph, queue, minimal, markedVertices, lookupTable);
        LogLookupTable(lookupTable);
        return true;
    }

    private bool MappingStep(ReadOnlySpan<TKey> keys, ulong seed, uint numPartitions, uint numEdges, Graph graph, uint[] queue, byte[] markedEdge, HashCode3<TKey> hashCode)
    {
        LogMappingStep(keys.Length, seed, numPartitions, numEdges);

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

    private int GenerateQueue(uint numEdges, uint[] queue, byte[] markedEdge, Graph graph)
    {
        uint v0;
        uint v1;
        uint v2;
        uint queueHead = 0;
        uint queueTail = 0;
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

        return (int)queueHead - (int)numEdges;
    }

    private void AssigningStep(uint numVertices, Graph graph, uint[] queue, bool minimal, byte[] markedVertices, byte[] lookupTable)
    {
        LogAssigningStep(queue.Length, numVertices);

        uint numEdges = graph.NumEdges;
        int lookupLength = (int)Math.Ceiling(numVertices / 4.0);

        if (minimal)
            Array.Fill<byte>(lookupTable, 0xff, 0, lookupLength);
        else
            Array.Clear(lookupTable, 0, lookupLength);

        Array.Fill<byte>(markedVertices, 0, 0, (int)((numVertices >> 3) + 1));
        bool isTraceEnabled = _logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace);

        for (uint i = numEdges - 1; i + 1 >= 1; i--)
        {
            uint currEdge = queue[i];
            uint v0 = graph.Edges[currEdge].Vertices[0];
            uint v1 = graph.Edges[currEdge].Vertices[1];
            uint v2 = graph.Edges[currEdge].Vertices[2];

            if (isTraceEnabled)
                LogEntryB(v0, v1, v2, GetValue(lookupTable, v0), GetValue(lookupTable, v1), GetValue(lookupTable, v2), currEdge);

            if (!GetBit(markedVertices, v0))
            {
                if (!GetBit(markedVertices, v1))
                {
                    if (!minimal)
                        SetValue1(lookupTable, v1, BdzShared.Unassigned);

                    SetBit(markedVertices, v1);
                }

                if (!GetBit(markedVertices, v2))
                {
                    if (!minimal)
                        SetValue1(lookupTable, v2, BdzShared.Unassigned);

                    SetBit(markedVertices, v2);
                }

                if (minimal)
                    SetValue1(lookupTable, v0, (uint)((6 - (GetValue(lookupTable, v1) + GetValue(lookupTable, v2))) % 3));
                else
                    SetValue0(lookupTable, v0, (uint)((6 - (GetValue(lookupTable, v1) + GetValue(lookupTable, v2))) % 3));

                SetBit(markedVertices, v0);
            }
            else if (!GetBit(markedVertices, v1))
            {
                if (!GetBit(markedVertices, v2))
                {
                    if (!minimal)
                        SetValue1(lookupTable, v2, BdzShared.Unassigned);

                    SetBit(markedVertices, v2);
                }

                if (minimal)
                    SetValue1(lookupTable, v1, (uint)((7 - (GetValue(lookupTable, v0) + GetValue(lookupTable, v2))) % 3));
                else
                    SetValue0(lookupTable, v1, (uint)((7 - (GetValue(lookupTable, v0) + GetValue(lookupTable, v2))) % 3));

                SetBit(markedVertices, v1);
            }
            else
            {
                if (minimal)
                    SetValue1(lookupTable, v2, (uint)((8 - (GetValue(lookupTable, v0) + GetValue(lookupTable, v1))) % 3));
                else
                    SetValue0(lookupTable, v2, (uint)((8 - (GetValue(lookupTable, v0) + GetValue(lookupTable, v1))) % 3));

                SetBit(markedVertices, v2);
            }

            if (isTraceEnabled)
                LogEntryA(v0, v1, v2, GetValue(lookupTable, v0), GetValue(lookupTable, v1), GetValue(lookupTable, v2));
        }
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

    private uint[] RankingStep(byte[] lookupTable, uint indexInRank, uint rankTableLength)
    {
        LogRankingStep(lookupTable.Length, indexInRank, rankTableLength);

        uint offset = 0U;
        uint count = 0U;
        uint size = indexInRank >> 2;
        uint numBytesTotal = (uint)lookupTable.Length;
        uint[] rankTable = new uint[rankTableLength];

        byte[] table = BdzShared.LookupTable.Value;

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

    private sealed class BuildState : IBuildState
    {
        public uint NumEdges;
        public uint NumPartitions;
        public uint NumVertices;
        public Graph Graph = null!;
        public uint[] Queue = [];
        public byte[] MarkedEdge = [];
        public byte[] MarkedVertices = [];
        public byte[] LookupTable = [];

        public static bool TryCreate(int keysLength, double loadFactor, [NotNullWhen(true)]out BuildState? state)
        {
            uint numEdges = (uint)keysLength;
            uint numPartitions = (uint)Math.Ceiling((loadFactor * numEdges) / 3);

            if (numPartitions % 2 == 0)
                numPartitions++;

            if (numPartitions == 1)
                numPartitions = 3;

            uint numVertices = 3 * numPartitions;

            state = new BuildState
            {
                NumEdges = numEdges,
                NumPartitions = numPartitions,
                NumVertices = numVertices,
                Graph = new Graph(numEdges, numVertices),
                Queue = GC.AllocateUninitializedArray<uint>((int)numEdges),
                MarkedEdge = GC.AllocateUninitializedArray<byte>((int)((numEdges >> 3) + 1)),
                MarkedVertices = GC.AllocateUninitializedArray<byte>((int)((numVertices >> 3) + 1)),
                LookupTable = GC.AllocateUninitializedArray<byte>((int)Math.Ceiling(numVertices / 4.0))
            };

            return true;
        }

        public void Reset()
        {
            Array.Clear(MarkedEdge, 0, MarkedEdge.Length);
            Array.Clear(MarkedVertices, 0, MarkedVertices.Length);
        }
    }
}