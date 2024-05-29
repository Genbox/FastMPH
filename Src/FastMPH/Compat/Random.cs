namespace Genbox.FastMPH.Compat;

public static class RandomProvider
{
    private static readonly Random _rng = new Random(42);

    public static Random Random => _rng;
}