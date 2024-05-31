namespace Genbox.FastMPH.Internals.Helpers;

public static class RandomHelper
{
    private static readonly Random _rng = new Random(42);

    public static uint Next() => (uint)_rng.Next();
}