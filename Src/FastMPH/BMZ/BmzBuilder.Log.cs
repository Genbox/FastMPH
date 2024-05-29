using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.BMZ;

public partial class BmzBuilder<TKey>
{
    private readonly ILogger _logger;

    /// <summary>Construct a BmzBuilder</summary>
    public BmzBuilder(ILogger logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating BMZ with {numKeys} keys. Vertices = {vertices}")]
    private partial void LogCreating(int numKeys, double vertices);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Trying iteration {iteration}")]
    private partial void LogIteration(uint iteration);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting ordering step")]
    private partial void LogStartOrdering();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting searching step")]
    private partial void LogStartSearching();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Traversing non critical vertices")]
    private partial void LogTraversingNonCriticalVertices();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Restarting mapping step. {mapIterations} iterations remaining")]
    private partial void LogRestartMappingStep(uint mapIterations);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Visiting neighbor {neighbor}")]
    private partial void LogVisitingNeighbor(uint neighbor);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Generating edges for {numVertices} vertices")]
    private partial void LogGeneratingEdges(uint numVertices);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Self-loop detected for key: {key} h1: {h1} h2: {h2}")]
    private partial void LogSelfLoop(string? key, uint h1, uint h2);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Adding edge for {h1} -> {h2}")]
    private partial void LogAddingEdge(string? key, uint h1, uint h2);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Non-simple graph was generated")]
    private partial void LogNonSimpleGraph();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully generated BMZ hash function")]
    private partial void LogSuccess();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to generated BMZ hash function")]
    private partial void LogFailure();
}