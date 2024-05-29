using System.Diagnostics;
using Genbox.FastMPH.Internals.Compat;
using Microsoft.Extensions.Logging;
using static Genbox.FastMPH.Internals.BitArray;

namespace Genbox.FastMPH.Internals;

internal sealed partial class Graph
{
    public const uint GraphNoNeighbor = uint.MaxValue;
    private const uint Empty = uint.MaxValue;

    private readonly uint _nodeCount;
    private readonly uint _edgeCount;
    private readonly uint[] _edges;
    private readonly uint[] _first;
    private readonly uint[] _next;

    private byte[]? _criticalNodes;
    private uint _criticalNodesCount;
    private uint _edgeCountOld;

    public Graph(ILogger logger, uint numNodes, uint numEdges)
    {
        _logger = logger;

        LogCreating(numNodes, numEdges);

        _nodeCount = numNodes;
        _edgeCount = numEdges;

        _edges = new uint[2 * _edgeCount];
        _next = new uint[2 * _edgeCount];
        _first = new uint[_nodeCount];
    }

    public uint GetEdgeId(uint v1, uint v2)
    {
        uint e = _first[v1];

        Debug.Assert(e != Empty);

        if (CheckEdge(e, v1, v2))
            return AbsEdge(e, 0);

        do
        {
            e = _next[e];
            Debug.Assert(e != Empty);
        } while (!CheckEdge(e, v1, v2));

        return AbsEdge(e, 0);
    }

    public bool IsCyclic()
    {
        byte[] deleted = new byte[_edgeCount / 8 + 1];

        for (uint v = 0; v < _nodeCount; ++v)
            DeleteCyclicEdge(v, deleted);

        for (uint i = 0; i < _edgeCount; ++i)
        {
            if (!GetBit(deleted, i))
                return true;
        }

        return false;
    }

    public void ObtainCriticalNodes()
    {
        byte[] deleted = new byte[_edgeCount / 8 + 1];
        _criticalNodes = new byte[_nodeCount / 8 + 1];
        Array2.Clear(_criticalNodes);
        _criticalNodesCount = 0;

        LogCriticalNodes(_nodeCount, _edgeCount);

        for (uint v = 0; v < _nodeCount; ++v)
            DeleteCyclicEdge(v, deleted);

        for (uint i = 0; i < _edgeCount; ++i)
        {
            if (!GetBit(deleted, i))
            {
                LogBelong2Core(i, _edges[i], _edges[i + _edgeCount]);

                if (!GetBit(_criticalNodes, _edges[i]))
                {
                    _criticalNodesCount++;
                    SetBit(_criticalNodes, _edges[i]);
                }

                if (!GetBit(_criticalNodes, _edges[i + _edgeCount]))
                {
                    _criticalNodesCount++;
                    SetBit(_criticalNodes, _edges[i + _edgeCount]);
                }
            }
        }
    }

    public bool NodeIsCritical(uint v) => GetBit(_criticalNodes!, v);

    private void DeleteCyclicEdge(uint v1, byte[] deleted)
    {
        bool degree1 = FindFirstDegreeEdge(v1, deleted, out uint e);

        if (!degree1)
            return;

        while (true)
        {
            LogDeletingEdge(e, _edges[AbsEdge(e, 0)], _edges[AbsEdge(e, 1)]);
            SetBit(deleted, AbsEdge(e, 0));

            uint v2 = _edges[AbsEdge(e, 0)];

            if (v2 == v1)
                v2 = _edges[AbsEdge(e, 1)];

            LogCheckingSecondEndpoint(v2);
            degree1 = FindFirstDegreeEdge(v2, deleted, out e);

            if (degree1)
            {
                LogInspectingVertex(v2);
                v1 = v2;
            }
            else
                break;
        }
    }

    private bool FindFirstDegreeEdge(uint v, byte[] deleted, out uint e)
    {
        e = 0;

        uint edge = _first[v];
        bool found = false;

        LogCheckingDegree(v, edge);

        if (edge == Empty)
            return false;

        if (!GetBit(deleted, AbsEdge(edge, 0)))
        {
            found = true;
            e = edge;
        }

        while (true)
        {
            edge = _next[edge];

            if (edge == Empty)
                break;

            if (GetBit(deleted, AbsEdge(edge, 0)))
                continue;

            if (found)
                return false;

            LogFoundFirstEdge();

            e = edge;
            found = true;
        }

        return found;
    }

    private uint AbsEdge(uint e, uint i) => e % _edgeCount + i * _edgeCount;

    public void ClearEdges()
    {
        for (int i = 0; i < _nodeCount; ++i)
            _first[i] = Empty;

        for (int i = 0; i < _edgeCount * 2; ++i)
        {
            _edges[i] = Empty;
            _next[i] = Empty;
        }

        _edgeCountOld = 0;
    }

    public GraphIterator GetGraphIterator(uint v) => new GraphIterator(v, _first[v]);

    public uint NextNeighbor(GraphIterator it)
    {
        if (it.Edge == Empty)
            return GraphNoNeighbor;

        //Genbox: converted this into a ?: expression
        uint ret = _edges[it.Edge] == it.Vertex ? _edges[it.Edge + _edgeCount] : _edges[it.Edge];

        it.Edge = _next[it.Edge];
        return ret;
    }

    public uint GetNumCriticalNodes() => _criticalNodesCount;

    public uint GetVertexId(uint e, uint id) => _edges[e + id * _edgeCount];

    public bool ContainsEdge(uint v1, uint v2)
    {
        uint e = _first[v1];

        if (e == Empty)
            return false;

        if (CheckEdge(e, v1, v2))
            return true;

        do
        {
            e = _next[e];
            if (e == Empty)
                return false;
        } while (!CheckEdge(e, v1, v2));
        return true;
    }

    private bool CheckEdge(uint e, uint v1, uint v2)
    {
        LogCheckEdge(_edges[AbsEdge(e, 0)], _edges[AbsEdge(e, 1)], v1, v2);

        if (_edges[AbsEdge(e, 0)] == v1 && _edges[AbsEdge(e, 1)] == v2)
            return true;

        if (_edges[AbsEdge(e, 0)] == v2 && _edges[AbsEdge(e, 1)] == v1)
            return true;

        return false;
    }

    public void AddEdge(uint v1, uint v2)
    {
        uint e = _edgeCountOld;

        Debug.Assert(v1 < _nodeCount);
        Debug.Assert(v2 < _nodeCount);
        Debug.Assert(e < _edgeCount);

        _next[e] = _first[v1];
        _first[v1] = e;
        _edges[e] = v2;

        _next[e + _edgeCount] = _first[v2];
        _first[v2] = e + _edgeCount;
        _edges[e + _edgeCount] = v1;

        ++_edgeCountOld;
    }
}