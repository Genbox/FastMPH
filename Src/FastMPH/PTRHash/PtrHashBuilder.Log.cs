using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.PTRHash;

public sealed partial class PtrHashBuilder<TKey> where TKey : notnull
{
    private readonly ILogger<PtrHashBuilder<TKey>> _logger;

    /// <summary>Construct a PTRHash builder.</summary>
    public PtrHashBuilder(ILogger<PtrHashBuilder<TKey>> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating PTRHash with {numKeys} keys. alpha={alpha} lambda={lambda}")]
    private partial void LogCreating(int numKeys, double alpha, double lambda);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Trying attempt {attempt} with seed {seed}. buckets={buckets} slots={slots}")]
    private partial void LogAttempt(uint attempt, uint seed, uint buckets, uint slots);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Failed placing bucket {bucket} of size {size}")]
    private partial void LogBucketFailure(int bucket, int size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully created PTRHash with seed {seed}")]
    private partial void LogSuccess(uint seed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to create PTRHash")]
    private partial void LogFailure();
}