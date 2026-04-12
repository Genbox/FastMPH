#if NETSTANDARD2_0
namespace System;

internal static class MathExtensions
{
    extension(Math)
    {
        public static ulong BigMul(ulong a, ulong b, out ulong low)
        {
            ulong a0 = (uint)a;
            ulong a1 = a >> 32;
            ulong b0 = (uint)b;
            ulong b1 = b >> 32;

            ulong p11 = a1 * b1;
            ulong p01 = a0 * b1;
            ulong p10 = a1 * b0;
            ulong p00 = a0 * b0;

            ulong middle = (p00 >> 32) + (uint)p01 + (uint)p10;
            low = (middle << 32) | (uint)p00;
            return p11 + (p01 >> 32) + (p10 >> 32) + (middle >> 32);
        }
    }
}
#endif