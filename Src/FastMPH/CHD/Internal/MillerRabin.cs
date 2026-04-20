namespace Genbox.FastMPH.CHD.Internal;

internal static class MillerRabin
{
    public static bool IsPrimeNumber(ulong n)
    {
        if (n < 2)
            return false;

        if (n == 2 || n == 3 || n == 5 || n == 7)
            return true;

        if (n % 2 == 0)
            return false;
        if (n % 3 == 0)
            return false;
        if (n % 5 == 0)
            return false;
        if (n % 7 == 0)
            return false;

        ulong s = 0;
        ulong d = n - 1;

        do
        {
            s++;
            d /= 2;
        } while (d % 2 == 0);

        ulong a = 2;
        if (!CheckWitness(IntPow(a, d, n), n, s))
            return false;
        a = 7;
        if (!CheckWitness(IntPow(a, d, n), n, s))
            return false;
        a = 61;
        return CheckWitness(IntPow(a, d, n), n, s);
    }

    private static ulong IntPow(ulong a, ulong d, ulong n)
    {
        ulong aPow = a;
        ulong res = 1;
        while (d > 0)
        {
            if ((d & 1) == 1)
                res = (res * aPow) % n;
            aPow = (aPow * aPow) % n;
            d /= 2;
        }
        return res;
    }

    private static bool CheckWitness(ulong aExpD, ulong n, ulong s)
    {
        ulong aExp = aExpD;
        if (aExp == 1 || aExp == n - 1)
            return true;

        for (ulong i = 1; i < s; ++i)
        {
            aExp = (aExp * aExp) % n;
            if (aExp == n - 1)
                return true;
        }
        return false;
    }
}