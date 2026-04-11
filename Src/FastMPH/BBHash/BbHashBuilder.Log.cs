using Microsoft.Extensions.Logging;

namespace Genbox.FastMPH.BBHash;

public sealed partial class BbHashBuilder<TKey> where TKey : notnull
{
    private readonly ILogger<BbHashBuilder<TKey>> _logger;

    /// <summary>Construct a BBHash builder.</summary>
    public BbHashBuilder(ILogger<BbHashBuilder<TKey>> logger) => _logger = logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating BBHash with {numKeys} keys. Gamma={gamma} MaxLevels={maxLevels}")]
    private partial void LogCreating(int numKeys, double gamma, uint maxLevels);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Starting level {level}. Remaining keys: {remaining}. Domain size: {domain}")]
    private partial void LogLevelStart(uint level, int remaining, uint domain);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Level {level} placed {placed} keys. Remaining keys: {remaining}")]
    private partial void LogLevelResult(uint level, int placed, int remaining);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully created BBHash with {numLevels} levels")]
    private partial void LogSuccess(int numLevels);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to create BBHash within configured levels")]
    private partial void LogFailure();
}