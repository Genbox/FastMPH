#if NETSTANDARD2_0
namespace System.Numerics;

internal static class BitOperations
{
    public static int TrailingZeroCount(ulong value)
    {
        if (value == 0)
            return 64;

        int count = 0;

        if ((value & 0xffffffffUL) == 0)
        {
            value >>= 32;
            count += 32;
        }

        if ((value & 0xffffUL) == 0)
        {
            value >>= 16;
            count += 16;
        }

        if ((value & 0xffUL) == 0)
        {
            value >>= 8;
            count += 8;
        }

        if ((value & 0xfUL) == 0)
        {
            value >>= 4;
            count += 4;
        }

        if ((value & 0x3UL) == 0)
        {
            value >>= 2;
            count += 2;
        }

        if ((value & 0x1UL) == 0)
            count += 1;

        return count;
    }

    public static int Log2(uint value)
    {
        int result = 0;

        if (value >= 1U << 16)
        {
            value >>= 16;
            result += 16;
        }

        if (value >= 1U << 8)
        {
            value >>= 8;
            result += 8;
        }

        if (value >= 1U << 4)
        {
            value >>= 4;
            result += 4;
        }

        if (value >= 1U << 2)
        {
            value >>= 2;
            result += 2;
        }

        if (value >= 1U << 1)
            result += 1;

        return result;
    }

    public static uint RoundUpToPowerOf2(uint value)
    {
        if (value == 0)
            return 0;

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        value++;

        if (value == 0)
            return 0;

        return value;
    }

    public static int PopCount(uint value)
    {
        value -= (value >> 1) & 0x55555555u;
        value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
        value = (value + (value >> 4)) & 0x0F0F0F0Fu;
        return (int)((value * 0x01010101u) >> 24);
    }
}
#endif
