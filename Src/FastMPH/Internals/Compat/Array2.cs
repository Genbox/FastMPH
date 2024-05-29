namespace Genbox.FastMPH.Internals.Compat;

internal static class Array2
{
    public static void Fill<T>(T[] array, T value)
    {
        Fill(array, value, 0, array.Length);
    }

    public static void Fill<T>(T[] array, T value, int startIndex, int count)
    {
        for (int i = startIndex; i < startIndex + count; i++)
        {
            array[i] = value;
        }
    }

    public static void Clear(Array arr) => Array.Clear(arr, 0, arr.Length);
}