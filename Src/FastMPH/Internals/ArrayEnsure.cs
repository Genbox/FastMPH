namespace Genbox.FastMPH.Internals;

internal static class ArrayEnsure
{
    public static void EnsureCapacity<T>(ref T[] array, int size)
    {
        if (array.Length < size)
            array = new T[size];
    }
}