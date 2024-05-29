using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.CHD;

public sealed partial class ChdBuilder<TKey>
{
    private readonly ILogger<ChdBuilder<TKey>> _logger;

    /// <summary>Construct a ChdBuilder</summary>
    public ChdBuilder(ILogger<ChdBuilder<TKey>> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating CHD with {numKeys} keys. NumBuckets = {numBuckets}. LoadFactor = {loadFactor}. Keys per bin = {keysPerBin}. Keys per bucket = {keysPerBucket}")]
    private partial void LogCreating(uint numKeys, uint numBuckets, double loadFactor, byte keysPerBin, byte keysPerBucket);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting mapping step for mph creation of {numItems} keys with {numBins} bins")]
    private partial void LogMappingStep(uint numItems, uint numBins);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting ordering step")]
    private partial void LogOrderingStep();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting searching step")]
    private partial void LogSearchingStep();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting compressing step")]
    private partial void LogCompressingStep();

    [LoggerMessage(Level = LogLevel.Trace, Message = "MAX BUCKET SIZE = {maxBucketSize}")]
    private partial void LogMaxBucketSize(uint maxBucketSize);

    [LoggerMessage(Level = LogLevel.Trace, Message = "USING HEURISTIC TO PLACE BUCKETS")]
    private partial void LogUsingHeuristics();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to create CHD")]
    private partial void LogFailed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully created CHD with seed {seed}")]
    private partial void LogSuccess(uint seed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BUCKET {currentBucket} PLACED --- DISPLACEMENT = {displacement}")]
    private partial void LogDisplacement(uint currentBucket, uint displacement);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BUCKET {currentBucket} NOT PLACED")]
    private partial void LogNotPlaced(uint currentBucket);
}