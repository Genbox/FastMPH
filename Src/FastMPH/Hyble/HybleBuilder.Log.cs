using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.Hyble;

public sealed partial class HybleBuilder<TKey> where TKey : notnull
{
    private readonly ILogger<HybleBuilder<TKey>> _logger;

    /// <summary>Construct a Hyble builder.</summary>
    public HybleBuilder(ILogger<HybleBuilder<TKey>> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating Hyble with {numKeys} keys. approxRange={approxRange} bucketCount={bucketCount}")]
    private partial void LogCreating(uint numKeys, uint approxRange, uint bucketCount);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Trying attempt {attempt} with seed {seed}")]
    private partial void LogAttempt(uint attempt, uint seed);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Failed placing bucket {bucket} of size {size}")]
    private partial void LogBucketFailure(int bucket, int size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully created Hyble with seed {seed}. capacity={capacity}")]
    private partial void LogSuccess(ulong seed, uint capacity);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to create Hyble")]
    private partial void LogFailure();
}