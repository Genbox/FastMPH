using Genbox.FastMPH.Internals;
using static Genbox.FastMPH.CHD.Internal.BitBool;

namespace Genbox.FastMPH.CHD.Internal;

internal sealed class CompressedSequence
{
    private readonly uint[] _lengthRems;
    private readonly uint _remR;
    private readonly Select _sel;
    private readonly uint[] _storeTable;
    private readonly uint _totalLength; // total length in bits of stored_table

    public CompressedSequence(uint[] valsTable, uint n)
    {
        // lengths: represents lengths of encoded values
        uint[] lengths = new uint[n];

        _totalLength = 0;

        for (int i = 0; i < n; i++)
        {
            if (valsTable[i] == 0)
                lengths[i] = 0;
            else
            {
                lengths[i] = (uint)Math.Log(valsTable[i] + 1, 2);
                _totalLength += lengths[i];
            }
        }

        _storeTable = new uint[(_totalLength + 31) >> 5];
        _totalLength = 0;

        for (int i = 0; i < n; i++)
        {
            if (valsTable[i] == 0)
                continue;

            uint storedValue = valsTable[i] - ((1U << (int)lengths[i]) - 1U);
            SetBitsAtPos(_storeTable, _totalLength, storedValue, lengths[i]);
            _totalLength += lengths[i];
        }

        _remR = (uint)Math.Log(_totalLength / n, 2);

        if (_remR == 0)
            _remR = 1;

        _lengthRems = new uint[GetBitsTableSize(n, _remR)];

        uint remsMask = (1U << (int)_remR) - 1U;
        _totalLength = 0;

        for (uint i = 0; i < n; i++)
        {
            _totalLength += lengths[i];
            SetBitsValue(_lengthRems, i, _totalLength & remsMask, _remR, remsMask);
            lengths[i] = _totalLength >> (int)_remR;
        }

        // FABIANO: before it was (cs->total_length >> cs->rem_r) + 1. But I wiped out the + 1 because
        // I changed the select structure to work up to m, instead of up to m - 1.
        _sel = new Select(lengths, n, _totalLength >> (int)_remR);
    }

    private CompressedSequence(uint remR, uint totalLength, uint[] lengthRems, uint[] storeTable, Select sel)
    {
        _remR = remR;
        _totalLength = totalLength;
        _lengthRems = lengthRems;
        _storeTable = storeTable;
        _sel = sel;
    }

    public uint Query(uint idx)
    {
        uint encIdx;
        uint selRes;

        // assert(idx < cs->n); // FABIANO ADDED
        uint remsMask = (1U << (int)_remR) - 1U;

        if (idx == 0)
        {
            encIdx = 0;
            selRes = _sel.Query(idx);
        }
        else
        {
            selRes = _sel.Query(idx - 1);

            encIdx = (selRes - (idx - 1)) << (int)_remR;
            encIdx += GetBitsValue(_lengthRems, idx - 1, _remR, remsMask);

            selRes = _sel.NextQuery(selRes);
        }

        uint encLength = (selRes - idx) << (int)_remR;
        encLength += GetBitsValue(_lengthRems, idx, _remR, remsMask);
        encLength -= encIdx;

        if (encLength == 0)
            return 0;

        uint storedValue = GetBitsAtPos(_storeTable, encIdx, encLength);
        return storedValue + ((1U << (int)encLength) - 1U);
    }

    public uint GetPackedSize() => sizeof(uint) + // _remR
                                   sizeof(uint) + // _totalLength
                                   sizeof(uint) + // _lengthRems length
                                   (sizeof(uint) * (uint)_lengthRems.Length) + // _lengthRems
                                   sizeof(uint) + // _storeTable length
                                   (sizeof(uint) * (uint)_storeTable.Length) + // _storeTable
                                   _sel.GetPackedSize(); //_sel

    public void Pack(SpanWriter writer)
    {
        writer.WriteUInt32(_remR);
        writer.WriteUInt32(_totalLength);
        writer.WriteUInt32((uint)_lengthRems.Length);

        foreach (uint u in _lengthRems)
            writer.WriteUInt32(u);

        writer.WriteUInt32((uint)_storeTable.Length);

        foreach (uint u in _storeTable)
            writer.WriteUInt32(u);

        _sel.Pack(writer);
    }

    public static CompressedSequence Unpack(SpanReader sr)
    {
        uint remR = sr.ReadUInt32();
        uint totalLength = sr.ReadUInt32();
        uint lengthRemsLen = sr.ReadUInt32();

        uint[] lengthRems = new uint[lengthRemsLen];

        for (int i = 0; i < lengthRemsLen; i++)
            lengthRems[i] = sr.ReadUInt32();

        uint storeTableLen = sr.ReadUInt32();

        uint[] storeTable = new uint[storeTableLen];

        for (int i = 0; i < storeTableLen; i++)
            storeTable[i] = sr.ReadUInt32();

        Select sel = Select.Unpack(sr);

        return new CompressedSequence(remR, totalLength, lengthRems, storeTable, sel);
    }
}