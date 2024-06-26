using System.Runtime.InteropServices;
using Genbox.FastMPH.Internals;

namespace Genbox.FastMPH.CHD.Internal;

internal sealed class Select
{
    private const int NbitsStepSelectTable = 7;
    private const int StepSelectTable = 128;
    private const int MaskStepSelectTable = 127;

    private static readonly Lazy<byte[]> LookupTable0 = new Lazy<byte[]>(() =>
    [
        0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 4, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 5, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 5, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 6, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 5, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 6, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 6, 3, 4, 4, 5, 4, 5, 5, 6, 4, 5, 5, 6, 5, 6, 6, 7, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 5, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 6, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 6, 3, 4, 4, 5, 4, 5, 5, 6, 4, 5, 5, 6, 5, 6, 6, 7, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 6, 3, 4, 4, 5, 4, 5, 5, 6, 4, 5, 5, 6, 5, 6, 6, 7, 3, 4, 4, 5, 4, 5, 5, 6, 4, 5, 5, 6, 5, 6, 6, 7, 4, 5, 5, 6, 5, 6, 6, 7, 5, 6, 6, 7, 6, 7, 7, 8
    ]);

    private static readonly Lazy<byte[][]> LookupTable1 = new Lazy<byte[][]>(() =>
    [
        [255, 255, 255, 255, 255, 255, 255, 255], [0, 255, 255, 255, 255, 255, 255, 255],
        [1, 255, 255, 255, 255, 255, 255, 255], [0, 1, 255, 255, 255, 255, 255, 255],
        [2, 255, 255, 255, 255, 255, 255, 255], [0, 2, 255, 255, 255, 255, 255, 255],
        [1, 2, 255, 255, 255, 255, 255, 255], [0, 1, 2, 255, 255, 255, 255, 255],
        [3, 255, 255, 255, 255, 255, 255, 255], [0, 3, 255, 255, 255, 255, 255, 255],
        [1, 3, 255, 255, 255, 255, 255, 255], [0, 1, 3, 255, 255, 255, 255, 255],
        [2, 3, 255, 255, 255, 255, 255, 255], [0, 2, 3, 255, 255, 255, 255, 255],
        [1, 2, 3, 255, 255, 255, 255, 255], [0, 1, 2, 3, 255, 255, 255, 255],
        [4, 255, 255, 255, 255, 255, 255, 255], [0, 4, 255, 255, 255, 255, 255, 255],
        [1, 4, 255, 255, 255, 255, 255, 255], [0, 1, 4, 255, 255, 255, 255, 255],
        [2, 4, 255, 255, 255, 255, 255, 255], [0, 2, 4, 255, 255, 255, 255, 255],
        [1, 2, 4, 255, 255, 255, 255, 255], [0, 1, 2, 4, 255, 255, 255, 255],
        [3, 4, 255, 255, 255, 255, 255, 255], [0, 3, 4, 255, 255, 255, 255, 255],
        [1, 3, 4, 255, 255, 255, 255, 255], [0, 1, 3, 4, 255, 255, 255, 255],
        [2, 3, 4, 255, 255, 255, 255, 255], [0, 2, 3, 4, 255, 255, 255, 255],
        [1, 2, 3, 4, 255, 255, 255, 255], [0, 1, 2, 3, 4, 255, 255, 255],
        [5, 255, 255, 255, 255, 255, 255, 255], [0, 5, 255, 255, 255, 255, 255, 255],
        [1, 5, 255, 255, 255, 255, 255, 255], [0, 1, 5, 255, 255, 255, 255, 255],
        [2, 5, 255, 255, 255, 255, 255, 255], [0, 2, 5, 255, 255, 255, 255, 255],
        [1, 2, 5, 255, 255, 255, 255, 255], [0, 1, 2, 5, 255, 255, 255, 255],
        [3, 5, 255, 255, 255, 255, 255, 255], [0, 3, 5, 255, 255, 255, 255, 255],
        [1, 3, 5, 255, 255, 255, 255, 255], [0, 1, 3, 5, 255, 255, 255, 255],
        [2, 3, 5, 255, 255, 255, 255, 255], [0, 2, 3, 5, 255, 255, 255, 255],
        [1, 2, 3, 5, 255, 255, 255, 255], [0, 1, 2, 3, 5, 255, 255, 255],
        [4, 5, 255, 255, 255, 255, 255, 255], [0, 4, 5, 255, 255, 255, 255, 255],
        [1, 4, 5, 255, 255, 255, 255, 255], [0, 1, 4, 5, 255, 255, 255, 255],
        [2, 4, 5, 255, 255, 255, 255, 255], [0, 2, 4, 5, 255, 255, 255, 255],
        [1, 2, 4, 5, 255, 255, 255, 255], [0, 1, 2, 4, 5, 255, 255, 255],
        [3, 4, 5, 255, 255, 255, 255, 255], [0, 3, 4, 5, 255, 255, 255, 255],
        [1, 3, 4, 5, 255, 255, 255, 255], [0, 1, 3, 4, 5, 255, 255, 255],
        [2, 3, 4, 5, 255, 255, 255, 255], [0, 2, 3, 4, 5, 255, 255, 255],
        [1, 2, 3, 4, 5, 255, 255, 255], [0, 1, 2, 3, 4, 5, 255, 255],
        [6, 255, 255, 255, 255, 255, 255, 255], [0, 6, 255, 255, 255, 255, 255, 255],
        [1, 6, 255, 255, 255, 255, 255, 255], [0, 1, 6, 255, 255, 255, 255, 255],
        [2, 6, 255, 255, 255, 255, 255, 255], [0, 2, 6, 255, 255, 255, 255, 255],
        [1, 2, 6, 255, 255, 255, 255, 255], [0, 1, 2, 6, 255, 255, 255, 255],
        [3, 6, 255, 255, 255, 255, 255, 255], [0, 3, 6, 255, 255, 255, 255, 255],
        [1, 3, 6, 255, 255, 255, 255, 255], [0, 1, 3, 6, 255, 255, 255, 255],
        [2, 3, 6, 255, 255, 255, 255, 255], [0, 2, 3, 6, 255, 255, 255, 255],
        [1, 2, 3, 6, 255, 255, 255, 255], [0, 1, 2, 3, 6, 255, 255, 255],
        [4, 6, 255, 255, 255, 255, 255, 255], [0, 4, 6, 255, 255, 255, 255, 255],
        [1, 4, 6, 255, 255, 255, 255, 255], [0, 1, 4, 6, 255, 255, 255, 255],
        [2, 4, 6, 255, 255, 255, 255, 255], [0, 2, 4, 6, 255, 255, 255, 255],
        [1, 2, 4, 6, 255, 255, 255, 255], [0, 1, 2, 4, 6, 255, 255, 255],
        [3, 4, 6, 255, 255, 255, 255, 255], [0, 3, 4, 6, 255, 255, 255, 255],
        [1, 3, 4, 6, 255, 255, 255, 255], [0, 1, 3, 4, 6, 255, 255, 255],
        [2, 3, 4, 6, 255, 255, 255, 255], [0, 2, 3, 4, 6, 255, 255, 255],
        [1, 2, 3, 4, 6, 255, 255, 255], [0, 1, 2, 3, 4, 6, 255, 255],
        [5, 6, 255, 255, 255, 255, 255, 255], [0, 5, 6, 255, 255, 255, 255, 255],
        [1, 5, 6, 255, 255, 255, 255, 255], [0, 1, 5, 6, 255, 255, 255, 255],
        [2, 5, 6, 255, 255, 255, 255, 255], [0, 2, 5, 6, 255, 255, 255, 255],
        [1, 2, 5, 6, 255, 255, 255, 255], [0, 1, 2, 5, 6, 255, 255, 255],
        [3, 5, 6, 255, 255, 255, 255, 255], [0, 3, 5, 6, 255, 255, 255, 255],
        [1, 3, 5, 6, 255, 255, 255, 255], [0, 1, 3, 5, 6, 255, 255, 255],
        [2, 3, 5, 6, 255, 255, 255, 255], [0, 2, 3, 5, 6, 255, 255, 255],
        [1, 2, 3, 5, 6, 255, 255, 255], [0, 1, 2, 3, 5, 6, 255, 255],
        [4, 5, 6, 255, 255, 255, 255, 255], [0, 4, 5, 6, 255, 255, 255, 255],
        [1, 4, 5, 6, 255, 255, 255, 255], [0, 1, 4, 5, 6, 255, 255, 255],
        [2, 4, 5, 6, 255, 255, 255, 255], [0, 2, 4, 5, 6, 255, 255, 255],
        [1, 2, 4, 5, 6, 255, 255, 255], [0, 1, 2, 4, 5, 6, 255, 255],
        [3, 4, 5, 6, 255, 255, 255, 255], [0, 3, 4, 5, 6, 255, 255, 255],
        [1, 3, 4, 5, 6, 255, 255, 255], [0, 1, 3, 4, 5, 6, 255, 255],
        [2, 3, 4, 5, 6, 255, 255, 255], [0, 2, 3, 4, 5, 6, 255, 255],
        [1, 2, 3, 4, 5, 6, 255, 255], [0, 1, 2, 3, 4, 5, 6, 255],
        [7, 255, 255, 255, 255, 255, 255, 255], [0, 7, 255, 255, 255, 255, 255, 255],
        [1, 7, 255, 255, 255, 255, 255, 255], [0, 1, 7, 255, 255, 255, 255, 255],
        [2, 7, 255, 255, 255, 255, 255, 255], [0, 2, 7, 255, 255, 255, 255, 255],
        [1, 2, 7, 255, 255, 255, 255, 255], [0, 1, 2, 7, 255, 255, 255, 255],
        [3, 7, 255, 255, 255, 255, 255, 255], [0, 3, 7, 255, 255, 255, 255, 255],
        [1, 3, 7, 255, 255, 255, 255, 255], [0, 1, 3, 7, 255, 255, 255, 255],
        [2, 3, 7, 255, 255, 255, 255, 255], [0, 2, 3, 7, 255, 255, 255, 255],
        [1, 2, 3, 7, 255, 255, 255, 255], [0, 1, 2, 3, 7, 255, 255, 255],
        [4, 7, 255, 255, 255, 255, 255, 255], [0, 4, 7, 255, 255, 255, 255, 255],
        [1, 4, 7, 255, 255, 255, 255, 255], [0, 1, 4, 7, 255, 255, 255, 255],
        [2, 4, 7, 255, 255, 255, 255, 255], [0, 2, 4, 7, 255, 255, 255, 255],
        [1, 2, 4, 7, 255, 255, 255, 255], [0, 1, 2, 4, 7, 255, 255, 255],
        [3, 4, 7, 255, 255, 255, 255, 255], [0, 3, 4, 7, 255, 255, 255, 255],
        [1, 3, 4, 7, 255, 255, 255, 255], [0, 1, 3, 4, 7, 255, 255, 255],
        [2, 3, 4, 7, 255, 255, 255, 255], [0, 2, 3, 4, 7, 255, 255, 255],
        [1, 2, 3, 4, 7, 255, 255, 255], [0, 1, 2, 3, 4, 7, 255, 255],
        [5, 7, 255, 255, 255, 255, 255, 255], [0, 5, 7, 255, 255, 255, 255, 255],
        [1, 5, 7, 255, 255, 255, 255, 255], [0, 1, 5, 7, 255, 255, 255, 255],
        [2, 5, 7, 255, 255, 255, 255, 255], [0, 2, 5, 7, 255, 255, 255, 255],
        [1, 2, 5, 7, 255, 255, 255, 255], [0, 1, 2, 5, 7, 255, 255, 255],
        [3, 5, 7, 255, 255, 255, 255, 255], [0, 3, 5, 7, 255, 255, 255, 255],
        [1, 3, 5, 7, 255, 255, 255, 255], [0, 1, 3, 5, 7, 255, 255, 255],
        [2, 3, 5, 7, 255, 255, 255, 255], [0, 2, 3, 5, 7, 255, 255, 255],
        [1, 2, 3, 5, 7, 255, 255, 255], [0, 1, 2, 3, 5, 7, 255, 255],
        [4, 5, 7, 255, 255, 255, 255, 255], [0, 4, 5, 7, 255, 255, 255, 255],
        [1, 4, 5, 7, 255, 255, 255, 255], [0, 1, 4, 5, 7, 255, 255, 255],
        [2, 4, 5, 7, 255, 255, 255, 255], [0, 2, 4, 5, 7, 255, 255, 255],
        [1, 2, 4, 5, 7, 255, 255, 255], [0, 1, 2, 4, 5, 7, 255, 255],
        [3, 4, 5, 7, 255, 255, 255, 255], [0, 3, 4, 5, 7, 255, 255, 255],
        [1, 3, 4, 5, 7, 255, 255, 255], [0, 1, 3, 4, 5, 7, 255, 255],
        [2, 3, 4, 5, 7, 255, 255, 255], [0, 2, 3, 4, 5, 7, 255, 255],
        [1, 2, 3, 4, 5, 7, 255, 255], [0, 1, 2, 3, 4, 5, 7, 255],
        [6, 7, 255, 255, 255, 255, 255, 255], [0, 6, 7, 255, 255, 255, 255, 255],
        [1, 6, 7, 255, 255, 255, 255, 255], [0, 1, 6, 7, 255, 255, 255, 255],
        [2, 6, 7, 255, 255, 255, 255, 255], [0, 2, 6, 7, 255, 255, 255, 255],
        [1, 2, 6, 7, 255, 255, 255, 255], [0, 1, 2, 6, 7, 255, 255, 255],
        [3, 6, 7, 255, 255, 255, 255, 255], [0, 3, 6, 7, 255, 255, 255, 255],
        [1, 3, 6, 7, 255, 255, 255, 255], [0, 1, 3, 6, 7, 255, 255, 255],
        [2, 3, 6, 7, 255, 255, 255, 255], [0, 2, 3, 6, 7, 255, 255, 255],
        [1, 2, 3, 6, 7, 255, 255, 255], [0, 1, 2, 3, 6, 7, 255, 255],
        [4, 6, 7, 255, 255, 255, 255, 255], [0, 4, 6, 7, 255, 255, 255, 255],
        [1, 4, 6, 7, 255, 255, 255, 255], [0, 1, 4, 6, 7, 255, 255, 255],
        [2, 4, 6, 7, 255, 255, 255, 255], [0, 2, 4, 6, 7, 255, 255, 255],
        [1, 2, 4, 6, 7, 255, 255, 255], [0, 1, 2, 4, 6, 7, 255, 255],
        [3, 4, 6, 7, 255, 255, 255, 255], [0, 3, 4, 6, 7, 255, 255, 255],
        [1, 3, 4, 6, 7, 255, 255, 255], [0, 1, 3, 4, 6, 7, 255, 255],
        [2, 3, 4, 6, 7, 255, 255, 255], [0, 2, 3, 4, 6, 7, 255, 255],
        [1, 2, 3, 4, 6, 7, 255, 255], [0, 1, 2, 3, 4, 6, 7, 255],
        [5, 6, 7, 255, 255, 255, 255, 255], [0, 5, 6, 7, 255, 255, 255, 255],
        [1, 5, 6, 7, 255, 255, 255, 255], [0, 1, 5, 6, 7, 255, 255, 255],
        [2, 5, 6, 7, 255, 255, 255, 255], [0, 2, 5, 6, 7, 255, 255, 255],
        [1, 2, 5, 6, 7, 255, 255, 255], [0, 1, 2, 5, 6, 7, 255, 255],
        [3, 5, 6, 7, 255, 255, 255, 255], [0, 3, 5, 6, 7, 255, 255, 255],
        [1, 3, 5, 6, 7, 255, 255, 255], [0, 1, 3, 5, 6, 7, 255, 255],
        [2, 3, 5, 6, 7, 255, 255, 255], [0, 2, 3, 5, 6, 7, 255, 255],
        [1, 2, 3, 5, 6, 7, 255, 255], [0, 1, 2, 3, 5, 6, 7, 255],
        [4, 5, 6, 7, 255, 255, 255, 255], [0, 4, 5, 6, 7, 255, 255, 255],
        [1, 4, 5, 6, 7, 255, 255, 255], [0, 1, 4, 5, 6, 7, 255, 255],
        [2, 4, 5, 6, 7, 255, 255, 255], [0, 2, 4, 5, 6, 7, 255, 255],
        [1, 2, 4, 5, 6, 7, 255, 255], [0, 1, 2, 4, 5, 6, 7, 255],
        [3, 4, 5, 6, 7, 255, 255, 255], [0, 3, 4, 5, 6, 7, 255, 255],
        [1, 3, 4, 5, 6, 7, 255, 255], [0, 1, 3, 4, 5, 6, 7, 255],
        [2, 3, 4, 5, 6, 7, 255, 255], [0, 2, 3, 4, 5, 6, 7, 255],
        [1, 2, 3, 4, 5, 6, 7, 255], [0, 1, 2, 3, 4, 5, 6, 7]
    ]);

    private readonly uint _m;
    private readonly uint _n;
    private readonly uint[] _selectTable;

    public readonly uint[] BitsVec;

    internal Select(uint[] keysVec, uint newN, uint newM)
    {
        uint i, j;
        uint buffer = 0;

        _n = newN;
        _m = newM; // n values in the range [0,m-1]

        uint nbits = _n + _m;
        uint vecSize = (nbits + 31) >> 5; // (nbits + 31) >> 5 = (nbits + 31)/32

        uint selTableSize = (_n >> NbitsStepSelectTable) + 1; // (sel->n >> NBITS_STEP_SELECT_TABLE) = (sel->n/STEP_SELECT_TABLE)

        BitsVec = new uint[vecSize];
        _selectTable = new uint[selTableSize];

        uint idx = i = j = 0;

        //Genbox: converted for loop to while loop
        while (true)
        {
            while (keysVec[j] == i)
            {
                Insert1(ref buffer);
                idx++;

                if ((idx & 0x1f) == 0) // (idx & 0x1f) = idx % 32
                    BitsVec[(idx >> 5) - 1] = buffer; // (idx >> 5) = idx/32

                j++;

                if (j == _n)
                    goto loop_end;
            }

            if (i == _m)
                break;

            while (keysVec[j] > i)
            {
                Insert0(ref buffer);
                idx++;

                if ((idx & 0x1f) == 0) // (idx & 0x1f) = idx % 32
                    BitsVec[(idx >> 5) - 1] = buffer; // (idx >> 5) = idx/32
                i++;
            }
        }

        loop_end:
        if ((idx & 0x1f) != 0) // (idx & 0x1f) = idx % 32
        {
            buffer >>= 32 - (int)(idx & 0x1f);
            BitsVec[(idx - 1) >> 5] = buffer;
        }

        GenerateTable();
    }

    private Select(uint n, uint m, uint[] selectTable, uint[] bitsVec)
    {
        _n = n;
        _m = m;
        _selectTable = selectTable;
        BitsVec = bitsVec;
    }

    private static void Insert0(ref uint buffer) => buffer >>= 1;

    private static void Insert1(ref uint buffer)
    {
        buffer >>= 1;
        buffer |= 0x80000000;
    }

    private void GenerateTable()
    {
        uint vecIdx, oneIdx, selTableIdx;
        uint partSum = vecIdx = oneIdx = selTableIdx = 0;

        Span<byte> bitsTable = MemoryMarshal.AsBytes(BitsVec.AsSpan());

        //Genbox: Lazy load tables
        byte[] lookupTable0 = LookupTable0.Value;
        byte[][] lookupTable1 = LookupTable1.Value;

        //Genbox: converted for loop to while loop
        while (true)
        {
            // FABIANO: Should'n it be one_idx >= sel->n
            if (oneIdx >= _n)
                break;
            uint oldPartSum;
            do
            {
                oldPartSum = partSum;
                partSum += lookupTable0[bitsTable[(int)vecIdx]];
                vecIdx++;
            } while (partSum <= oneIdx);

            _selectTable[selTableIdx] = lookupTable1[bitsTable[(int)(vecIdx - 1)]][oneIdx - oldPartSum] + ((vecIdx - 1) << 3); // ((vec_idx - 1) << 3) = ((vec_idx - 1) * 8)
            oneIdx += StepSelectTable;
            selTableIdx++;
        }
    }

    private uint Query(uint[] selectTable, uint oneIdx)
    {
        Span<byte> bitsTable = MemoryMarshal.AsBytes(BitsVec.AsSpan());

        uint vecBitIdx = selectTable[oneIdx >> NbitsStepSelectTable]; // one_idx >> NBITS_STEP_SELECT_TABLE = one_idx/STEP_SELECT_TABLE
        uint vecByteIdx = vecBitIdx >> 3; // vec_bit_idx / 8

        //Genbox: Lazy load tables
        byte[] lookupTable0 = LookupTable0.Value;
        byte[][] lookupTable1 = LookupTable1.Value;

        oneIdx &= MaskStepSelectTable; // one_idx %= STEP_SELECT_TABLE == one_idx &= MASK_STEP_SELECT_TABLE
        oneIdx += lookupTable0[bitsTable[(int)vecByteIdx] & ((1 << (int)(vecBitIdx & 0x7)) - 1)];
        uint partSum = 0;

        uint oldPartSum;
        do
        {
            oldPartSum = partSum;
            partSum += lookupTable0[bitsTable[(int)vecByteIdx]];
            vecByteIdx++;
        } while (partSum <= oneIdx);

        return lookupTable1[bitsTable[(int)(vecByteIdx - 1)]][oneIdx - oldPartSum] + ((vecByteIdx - 1) << 3);
    }

    public uint Query(uint oneIdx) => Query(_selectTable, oneIdx);

    public uint NextQuery(uint vecBitIdx)
    {
        Span<byte> bitsTable = MemoryMarshal.AsBytes(BitsVec.AsSpan());
        //Genbox: Lazy load tables
        byte[] lookupTable0 = LookupTable0.Value;
        byte[][] lookupTable1 = LookupTable1.Value;

        uint vecByteIdx = vecBitIdx >> 3;
        uint oneIdx = lookupTable0[bitsTable[(int)vecByteIdx] & ((1U << (int)(vecBitIdx & 0x7)) - 1U)] + 1U;
        uint partSum = 0;

        uint oldPartSum;
        do
        {
            oldPartSum = partSum;
            partSum += lookupTable0[bitsTable[(int)vecByteIdx]];
            vecByteIdx++;
        } while (partSum <= oneIdx);

        return lookupTable1[bitsTable[(int)(vecByteIdx - 1)]][oneIdx - oldPartSum] + ((vecByteIdx - 1) << 3);
    }

    public uint GetPackedSize() => sizeof(uint) + //_n
                                   sizeof(uint) + //_m
                                   sizeof(uint) + //_selectTable length
                                   (sizeof(uint) * (uint)_selectTable.Length) + //_selectTable
                                   sizeof(uint) + //BitsVec length
                                   (sizeof(uint) * (uint)BitsVec.Length); //BitsVec

    public void Pack(SpanWriter sw)
    {
        sw.WriteUInt32(_n);
        sw.WriteUInt32(_m);
        sw.WriteUInt32((uint)_selectTable.Length);

        foreach (uint u in _selectTable)
            sw.WriteUInt32(u);

        sw.WriteUInt32((uint)BitsVec.Length);

        foreach (uint u in BitsVec)
            sw.WriteUInt32(u);
    }

    public static Select Unpack(SpanReader sr)
    {
        uint n = sr.ReadUInt32();
        uint m = sr.ReadUInt32();
        uint selectTableLength = sr.ReadUInt32();

        uint[] selectTable = new uint[selectTableLength];

        for (int i = 0; i < selectTable.Length; i++)
            selectTable[i] = sr.ReadUInt32();

        uint bitsVecLength = sr.ReadUInt32();

        uint[] bitsVec = new uint[bitsVecLength];

        for (int i = 0; i < bitsVec.Length; i++)
            bitsVec[i] = sr.ReadUInt32();

        return new Select(n, m, selectTable, bitsVec);
    }
}