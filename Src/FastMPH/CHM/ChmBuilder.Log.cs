using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.CHM;

public partial class ChmBuilder<TKey>
{
    private readonly ILogger _logger;

    /// <summary>Construct a ChmBuilder</summary>
    public ChmBuilder(ILogger logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating CHM with {numKeys} keys. c = {c}")]
    private partial void LogCreating(int numKeys, double c);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Staring mapping step")]
    private partial void LogMappingStep();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Trying iteration {iteration}")]
    private partial void LogIteration(uint iteration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to generate function")]
    private partial void LogFailed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Staring assignment step")]
    private partial void LogAssignmentStep();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Visiting edge {nodeA}->{nodeB} with id {nodeId}")]
    private partial void LogVisitingEdge(uint nodeA, uint nodeB, uint nodeId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Visiting vertex {vertex}")]
    private partial void LogVisitingVertex(uint vertex);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Visiting neighbor {neighbor}")]
    private partial void LogVisitingNeighbor(uint neighbor);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Lookup is {neighbor} ({valueA} - {valueB})")]
    private partial void LogStatus(uint neighbor, uint valueA, uint valueB);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Self loop for key {key}")]
    private partial void LogSelfLoop(int key);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Adding edge: {h1} -> {h2}")]
    private partial void LogAddingEdge(uint h1, uint h2);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Cyclic graph created")]
    private partial void LogCyclicGraph();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully built a CHM hash function")]
    private partial void LogSuccess();
}