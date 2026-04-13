using JetBrains.Annotations;
using Genbox.FastMPH.Internals;

namespace Genbox.FastMPH;

[PublicAPI]
public sealed class RetryOptions
{
    public RetryOptions() {}

    public RetryOptions(uint maxAttempts, ulong initialSeed = 0x517CC1B727220A95UL, ulong seedStep = 0x9E3779B97F4A7C15UL)
    {
        MaxAttempts = maxAttempts;
        InitialSeed = initialSeed;
        SeedStep = seedStep;
    }

    public uint MaxAttempts
    {
        get;
        set
        {
            Validator.RequireThat(value > 0);
            field = value;
        }
    } = 256;

    public ulong InitialSeed { get; set; } = 0x517CC1B727220A95UL;
    public ulong SeedStep { get; set; } = 0x9E3779B97F4A7C15UL;
}