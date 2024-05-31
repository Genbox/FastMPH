using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.CHD.Internal;
using JetBrains.Annotations;

namespace Genbox.FastMPH.CHD;

/// <summary>Contains the state of a CHD minimal perfect hash function</summary>
[PublicAPI]
public sealed class ChdMinimalState<TKey> : IHashState<TKey> where TKey : notnull
{
    private readonly CompressedRank _rank;
    private readonly ChdState<TKey> _state;

    internal ChdMinimalState(ChdState<TKey> state, CompressedRank rank)
    {
        _state = state;
        _rank = rank;
    }

    /// <inheritdoc />
    public uint Search(TKey key)
    {
        uint idx = _state.Search(key);
        return idx - _rank.Query(idx);
    }

    /// <inheritdoc />
    public uint GetPackedSize() => _state.GetPackedSize() + _rank.GetPackedSize();

    /// <inheritdoc />
    public void Pack(Span<byte> buffer)
    {
        _state.Pack(buffer);
        buffer = buffer[(int)_state.GetPackedSize()..];
        _rank.Pack(buffer);
    }

    /// <summary>
    /// Deserialize a serialized minimal perfect hash function into a new instance of <see cref="ChdMinimalState{TKey}" />
    /// </summary>
    /// <param name="packed">The serialized hash function</param>
    public static ChdMinimalState<TKey> Unpack(ReadOnlySpan<byte> packed)
    {
        ChdState<TKey> state = ChdState<TKey>.Unpack(packed);
        packed = packed[(int)state.GetPackedSize()..];
        CompressedRank rank = CompressedRank.Unpack(packed);

        return new ChdMinimalState<TKey>(state, rank);
    }
}