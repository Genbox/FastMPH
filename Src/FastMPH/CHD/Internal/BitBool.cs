namespace Genbox.FastMPH.CHD.Internal;

internal static class BitBool
{
    //TODO: Benchmark where we calculate these on the fly
    private static readonly uint[] Bitmask32 =
    {
        1, 1 << 1, 1 << 2, 1 << 3, 1 << 4, 1 << 5, 1 << 6, 1 << 7,
        1 << 8, 1 << 9, 1 << 10, 1 << 11, 1 << 12, 1 << 13, 1 << 14, 1 << 15,
        1 << 16, 1 << 17, 1 << 18, 1 << 19, 1 << 20, 1 << 21, 1 << 22, 1 << 23,
        1 << 24, 1 << 25, 1 << 26, 1 << 27, 1 << 28, 1 << 29, 1 << 30, 1U << 31
    };

    public static uint GetBitsTableSize(uint n, uint bitsLength) => (n * bitsLength + 31) >> 5;

    public static void UnsetBit(Span<uint> array, uint i) => array[(int)(i >> 5)] ^= Bitmask32[i & 0x0000001f];

    public static void SetBit(Span<uint> array, uint i) => array[(int)(i >> 5)] |= Bitmask32[i & 0x0000001f];

    public static bool GetBit(ReadOnlySpan<uint> array, uint i) => (array[(int)(i >> 5)] & Bitmask32[i & 0x0000001f]) != 0;

    //TODO: Benchmark this with a span
    public static void SetBitsAtPos(uint[] bitsTable, uint pos, uint bitsString, uint stringLength)
    {
        uint wordIdx = pos >> 5;
        uint shift1 = pos & 0x0000001f;
        uint shift2 = 32 - shift1;
        uint stringMask = (uint)((1 << (int)stringLength) - 1);

        bitsTable[wordIdx] &= ~(stringMask << (int)shift1);
        bitsTable[wordIdx] |= bitsString << (int)shift1;

        if (shift2 < stringLength)
        {
            bitsTable[wordIdx + 1] &= ~(stringMask >> (int)shift2);
            bitsTable[wordIdx + 1] |= bitsString >> (int)shift2;
        }
    }

    //TODO: Benchmark this with a span
    public static uint GetBitsAtPos(uint[] bitsTable, uint pos, uint stringLength)
    {
        uint wordIdx = pos >> 5;
        uint shift1 = pos & 0x0000001f;
        uint shift2 = 32 - shift1;
        uint stringMask = (1U << (int)stringLength) - 1;
        uint bitsString = (bitsTable[wordIdx] >> (int)shift1) & stringMask;

        if (shift2 < stringLength)
            bitsString |= (bitsTable[wordIdx + 1] << (int)shift2) & stringMask;

        return bitsString;
    }

    //TODO: Benchmark this with a span
    public static void SetBitsValue(uint[] bitsTable, uint index, uint bitsString, uint stringLength, uint stringMask)
    {
        uint bitIdx = index * stringLength;
        uint wordIdx = bitIdx >> 5;
        uint shift1 = bitIdx & 0x0000001f;
        uint shift2 = 32 - shift1;

        bitsTable[wordIdx] &= ~(stringMask << (int)shift1);
        bitsTable[wordIdx] |= bitsString << (int)shift1;

        if (shift2 < stringLength)
        {
            bitsTable[wordIdx + 1] &= ~(stringMask >> (int)shift2);
            bitsTable[wordIdx + 1] |= bitsString >> (int)shift2;
        }
    }

    //TODO: Benchmark this with a span
    public static uint GetBitsValue(uint[] bitsTable, uint index, uint stringLength, uint stringMask)
    {
        uint bitIdx = index * stringLength;
        uint wordIdx = bitIdx >> 5;
        uint shift1 = bitIdx & 0x0000001f;
        uint shift2 = 32 - shift1;
        uint bitsString = (bitsTable[wordIdx] >> (int)shift1) & stringMask;

        if (shift2 < stringLength)
            bitsString |= (bitsTable[(int)(wordIdx + 1)] << (int)shift2) & stringMask;

        return bitsString;
    }
}