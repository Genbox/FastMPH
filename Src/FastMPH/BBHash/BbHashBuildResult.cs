using JetBrains.Annotations;

namespace Genbox.FastMPH.BBHash;

[PublicAPI]
public sealed class BbHashBuildResult<TKey>(BbHashMinimalState<TKey> state, Dictionary<TKey, uint> remainder) where TKey : notnull
{
    public BbHashMinimalState<TKey> State { get; } = state;

    public Dictionary<TKey, uint> Remainder { get; } = remainder;
}