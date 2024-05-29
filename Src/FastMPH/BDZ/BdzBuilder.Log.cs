using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.BDZ;

public partial class BdzBuilder<TKey>
{
    private readonly ILogger<BdzBuilder<TKey>> _logger;

    /// <summary>Construct a BdzBuilder</summary>
    public BdzBuilder(ILogger<BdzBuilder<TKey>> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating BDZ with {numKeys} keys. LoadFactor = {loadFactor}")]
    private partial void LogCreating(int numKeys, double loadFactor);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating BDZ with {numKeys} keys. LoadFactor = {loadFactor}, NumBitsOfKey = {numBitsOfKey}")]
    private partial void LogCreatingMinimal(int numKeys, double loadFactor, byte numBitsOfKey);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Adding edge with {v0} {v1} {v2}")]
    private partial void LogAddingEdge(uint v0, uint v1, uint v2);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Removing edge {edge}")]
    private partial void LogRemovingEdge(uint edge);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Queue head: {head}, Queue tail: {tail}")]
    private partial void LogQueueState(uint head, uint tail);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to create BDZ")]
    private partial void LogFailed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entering mapping step for with {keys} keys, {partitions} partitions, {edges} edges and seed {seed}")]
    private partial void LogMappingStep(int keys, uint seed, uint partitions, uint edges);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entering assigning step for with {queueLength} queue length and a graph of {vertices} vertices")]
    private partial void LogAssigningStep(int queueLength, uint vertices);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Entering ranking step with index {indexInRank}. Lookup table is {lookupTableLength} long and ranking table is {rankTableLength} long")]
    private partial void LogRankingStep(int lookupTableLength, uint indexInRank, uint rankTableLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully created BDZ with seed {seed} and {numPartitions} partitions")]
    private partial void LogSuccess(uint seed, uint numPartitions);

    [LoggerMessage(Level = LogLevel.Trace, Message = "A: {v0} {v1} {v2} -- {e0} {e1} {e2}")]
    private partial void LogEntryA(uint v0, uint v1, uint v2, uint e0, uint e1, uint e2);

    [LoggerMessage(Level = LogLevel.Trace, Message = "B: {v0} {v1} {v2} -- {e0} {e1} {e2} edge: {edge}")]
    private partial void LogEntryB(uint v0, uint v1, uint v2, uint e0, uint e1, uint e2, uint edge);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Created hyper graph with {numEdges} edges and {numVertices} vertices")]
    private partial void LogCreatedHyperGraph(uint numEdges, uint numVertices);

    [LoggerMessage(Level = LogLevel.Trace, Message = "LookupTable: {lookupTable}")]
    private partial void LogLookupTable(string lookupTable);

    [LoggerMessage(Level = LogLevel.Trace, Message = "RankTable: {rankTable}")]
    private partial void LogRankTable(string rankTable);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trying iteration {iteration}")]
    private partial void LogIteration(uint iteration);
}