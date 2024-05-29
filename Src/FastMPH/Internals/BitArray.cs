namespace Genbox.FastMPH.Internals;

internal static class BitArray
{
    private static readonly byte[] _bitMask = [1, 1 << 1, 1 << 2, 1 << 3, 1 << 4, 1 << 5, 1 << 6, 1 << 7];
    private static readonly byte[] _valueMask = [0xfc, 0xf3, 0xcf, 0x3f];

    public static bool GetBit(byte[] array, uint i) => (array[i >> 3] & _bitMask[i & 0x00000007]) >> ((int)i & 0x00000007) != 0;

    public static void SetBit(byte[] array, uint i) => array[i >> 3] |= _bitMask[i & 0x00000007];

    public static void SetValue1(byte[] array, uint i, uint v) => array[i >> 2] &= (byte)((v << (((int)i & 0x00000003) << 1)) | _valueMask[i & 0x00000003]);
    public static void SetValue0(byte[] array, uint i, uint v) => array[i >> 2] |= (byte)(v << (((int)i & 0x00000003) << 1));
    public static byte GetValue(byte[] array, uint i) => (byte)((array[i >> 2] >> (int)((i & 0x00000003U) << 1)) & 0x00000003U);
}