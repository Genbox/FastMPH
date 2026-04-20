#if NETSTANDARD2_0
// ReSharper disable CheckNamespace

namespace System;

internal static class ArrayExtensions
{
    extension(Array)
    {
        internal static void Fill<T>(T[] array, T value)
        {
            Fill(array, value, 0, array.Length);
        }

        internal static void Fill<T>(T[] array, T value, int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
                array[i] = value;
        }

        internal static void Clear<T>(T[] array)
        {
            Fill(array, default, 0, array.Length);
        }
    }
}
#endif