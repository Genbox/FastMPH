using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.Internals;

internal sealed partial class Graph
{
    private readonly ILogger _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating graph with {numNodes} nodes and {numEdges} edges")]
    private partial void LogCreating(uint numNodes, uint numEdges);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Looking for the 2-core in graph with {numNodes} vertices and {numEdges} edges")]
    private partial void LogCriticalNodes(uint numNodes, uint numEdges);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Edge {index} {edgeA}->{edgeB} belongs to the 2-core")]
    private partial void LogBelong2Core(uint index, uint edgeA, uint edgeB);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Checking degree of vertex {vertex} connected to edge {edge}")]
    private partial void LogCheckingDegree(uint vertex, uint edge);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Checking edge {edgeA} {edgeB} looking for {v1} {v2}")]
    private partial void LogCheckEdge(uint edgeA, uint edgeB, uint v1, uint v2);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting edge {edge} ({v1}->{v2})")]
    private partial void LogDeletingEdge(uint edge, uint v1, uint v2);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Checking if second endpoint {vertex} has degree 1")]
    private partial void LogCheckingSecondEndpoint(uint vertex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Inspecting vertex {vertex}")]
    private partial void LogInspectingVertex(uint vertex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found first edge")]
    private partial void LogFoundFirstEdge();
}