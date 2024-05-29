namespace Genbox.FastMPH.Internals;

internal static class Utils
{
    public static uint Log2(uint x)
    {
        uint res = 0;

        while (x > 1)
        {
            x >>= 1;
            res++;
        }
        return res;
    }
}