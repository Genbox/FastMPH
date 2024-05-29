using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.FCH;

public partial class FchBuilder<TKey> where TKey : notnull
{
    private readonly ILogger _logger;

    /// <summary>Construct a FchBuilder</summary>
    public FchBuilder(ILogger logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating FCH with {numKeys} keys. BitsPerKey = {bitsPerKey}")]
    private partial void LogCreating(uint numKeys, double bitsPerKey);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting mapping step with seed {seed}. b:{b}  p1:{p1}  p2:{p2}")]
    private partial void LogMappingStep(uint seed, uint b, double p1, double p2);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting ordering step")]
    private partial void LogOrderingStep();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting searching step")]
    private partial void LogSearchingStep();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to generate hash function")]
    private partial void LogFailed();

    [LoggerMessage(Level = LogLevel.Trace, Message = "g[{index}]: {value}")]
    private partial void LogSearchStatus(uint index, uint value);

    [LoggerMessage(Level = LogLevel.Trace, Message = "key:{key}  index:{index}  h2:{h2}  bucket size:{bucketSize}")]
    private partial void LogSearchStatus2(string? key, uint index, uint h2, uint bucketSize);

    [LoggerMessage(Level = LogLevel.Trace, Message = "bucket {bucket} -- nkeys: {numKeys}")]
    private partial void LogBucket(int bucket, uint numKeys);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Increasing current capacity {capacity} to {newCapacity}")]
    private static partial void LogIncreasingCapacity(ILogger logger, uint capacity, uint newCapacity);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully created FCH with seeds {seed0}, {seed1}")]
    private partial void LogSuccess(uint seed0, uint seed1);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Trying iteration {iteration}")]
    private partial void LogIteration(uint iteration);
}