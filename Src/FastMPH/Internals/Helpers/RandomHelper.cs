namespace Genbox.FastMPH.Internals.Helpers;

public static class RandomHelper
{
    private static readonly Random _rng = new Random(42);
    public static ulong Next64() => ((ulong)(uint)_rng.Next() << 32) | (uint)_rng.Next();
}