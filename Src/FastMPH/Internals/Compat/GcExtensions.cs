#if NETSTANDARD2_0
// ReSharper disable CheckNamespace

namespace System;

internal static class GcExtensions
{
    extension(GC)
    {
        internal static T[] AllocateUninitializedArray<T>(int length)
        {
            return new T[length];
        }
    }
}
#endif