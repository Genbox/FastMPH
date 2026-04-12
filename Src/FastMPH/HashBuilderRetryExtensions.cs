using System.Diagnostics.CodeAnalysis;
using Genbox.FastMPH.Abstracts;
using JetBrains.Annotations;

namespace Genbox.FastMPH;

[PublicAPI]
public static class HashBuilderRetryExtensions
{
    public static bool TryCreateWithRetry<TKey, TState, TSettings>(
        this IHashBuilder<TKey, TState, TSettings> builder,
        ReadOnlySpan<TKey> keys,
        Func<TKey, ulong> hashFunc,
        [NotNullWhen(true)]out TState? state,
        TSettings? settings = null,
        RetryOptions? options = null)
        where TKey : notnull
        where TState : IHashState<TKey>
        where TSettings : HashSettings
    {
        TSettings activeSettings = CreateSettings(settings);
        RetryOptions activeOptions = options ?? new RetryOptions();
        ValidateOptions(activeOptions);

        ulong seed = activeOptions.InitialSeed;

        for (uint i = 0; i < activeOptions.MaxAttempts; i++)
        {
            if (builder.TryCreate(keys, hashFunc, seed, out state, activeSettings))
                return true;

            seed = unchecked(seed + activeOptions.SeedStep);
        }

        state = default;
        return false;
    }

    public static bool TryCreateMinimalWithRetry<TKey, TState, TSettings>(
        this IMinimalHashBuilder<TKey, TState, TSettings> builder,
        ReadOnlySpan<TKey> keys,
        Func<TKey, ulong> hashFunc,
        [NotNullWhen(true)]out TState? state,
        TSettings? settings = null,
        RetryOptions? options = null)
        where TKey : notnull
        where TState : IHashState<TKey>
        where TSettings : HashSettings
    {
        TSettings activeSettings = CreateSettings(settings);
        RetryOptions activeOptions = options ?? new RetryOptions();
        ValidateOptions(activeOptions);

        ulong seed = activeOptions.InitialSeed;

        for (uint i = 0; i < activeOptions.MaxAttempts; i++)
        {
            if (builder.TryCreateMinimal(keys, hashFunc, seed, out state, activeSettings))
                return true;

            seed = unchecked(seed + activeOptions.SeedStep);
        }

        state = default;
        return false;
    }

    private static TSettings CreateSettings<TSettings>(TSettings? settings) where TSettings : HashSettings
    {
        if (settings != null)
            return settings;

        object? created = Activator.CreateInstance(typeof(TSettings));
        if (created is TSettings typed)
            return typed;

        throw new InvalidOperationException($"Unable to create settings instance for type '{typeof(TSettings).Name}'.");
    }

    private static void ValidateOptions(RetryOptions options)
    {
        if (options.MaxAttempts == 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxAttempts must be larger than zero.");
    }
}