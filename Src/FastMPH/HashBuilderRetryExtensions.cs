using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;
using JetBrains.Annotations;

namespace Genbox.FastMPH;

[PublicAPI]
public static class HashBuilderRetryExtensions
{
    public static bool TryCreateWithRetry<TKey, TState, TSettings>(
        this IHashBuilder<TKey, TState, TSettings> builder,
        ReadOnlySpan<TKey> keys,
        HashFunc<TKey> hashFunc,
        [NotNullWhen(true)]out TState? state,
        TSettings? settings = null,
        RetryOptions? options = null)
        where TKey : notnull
        where TState : IQueryState<TKey>
        where TSettings : HashSettings, new()
    {
        TSettings activeSettings = settings ?? new TSettings();
        RetryOptions activeOptions = options ?? new RetryOptions();

        if (!builder.TryCreateState(keys.Length, activeSettings, out IBuildState? buildState))
        {
            state = default;
            return false;
        }

        ulong seed = activeOptions.InitialSeed;

        for (uint i = 0; i < activeOptions.MaxAttempts; i++)
        {
            if (builder.TryCreateCore(keys, hashFunc, seed, buildState, activeSettings, out state))
                return true;

            buildState.Reset();
            seed = unchecked(seed + activeOptions.SeedStep);
        }

        state = default;
        return false;
    }

    public static bool TryCreateMinimalWithRetry<TKey, TState, TSettings>(
        this IMinimalHashBuilder<TKey, TState, TSettings> builder,
        ReadOnlySpan<TKey> keys,
        HashFunc<TKey> hashFunc,
        [NotNullWhen(true)]out TState? state,
        TSettings? settings = null,
        RetryOptions? options = null)
        where TKey : notnull
        where TState : IQueryState<TKey>
        where TSettings : HashSettings, new()
    {
        TSettings activeSettings = settings ?? new TSettings();
        RetryOptions activeOptions = options ?? new RetryOptions();

        if (!builder.TryCreateMinimalState(keys.Length, activeSettings, out IBuildState? buildState))
        {
            state = default;
            return false;
        }

        ulong seed = activeOptions.InitialSeed;

        for (uint i = 0; i < activeOptions.MaxAttempts; i++)
        {
            if (builder.TryCreateMinimalCore(keys, hashFunc, seed, buildState, activeSettings, out state))
                return true;

            buildState.Reset();
            seed = unchecked(seed + activeOptions.SeedStep);
        }

        state = default;
        return false;
    }
}