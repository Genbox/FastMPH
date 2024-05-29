using Genbox.FastMPH.Abstracts;
using Genbox.FastMPH.Internals;

namespace Genbox.FastMPH.CHD.Internal;

internal sealed class CompressedRank : IPackable
{
    private uint _maxVal;
    private uint _remR;
    private uint _valsRemsCount;
    private uint[] _valsRems;
    private readonly Select _sel;

    public CompressedRank() => _sel = new Select();

    private CompressedRank(uint maxVal, uint remR, uint valsRemsCount, uint[] valsRems, Select sel)
    {
        _maxVal = maxVal;
        _remR = remR;
        _valsRemsCount = valsRemsCount;
        _valsRems = valsRems;
        _sel = sel;
    }

    public void Generate(uint[] valsTable, uint num)
    {
        uint i, j;
        _valsRemsCount = num;
        _maxVal = valsTable[_valsRemsCount - 1];
        _remR = Utils.Log2(_maxVal / _valsRemsCount);

        if (_remR == 0)
            _remR = 1;

        uint[] selectVec = new uint[_maxVal >> (int)_remR];
        _valsRems = new uint[BitBool.GetBitsTableSize(_valsRemsCount, _remR)];
        uint remsMask = (1U << (int)_remR) - 1U;

        for (i = 0; i < _valsRemsCount; i++)
            BitBool.SetBitsValue(_valsRems, i, valsTable[i] & remsMask, _remR, remsMask);

        for (i = 1, j = 0; i <= _maxVal >> (int)_remR; i++)
        {
            while (i > valsTable[j] >> (int)_remR)
                j++;

            selectVec[i - 1] = j;
        }

        // FABIANO: before it was (cr->total_length >> cr->rem_r) + 1. But I wiped out the + 1 because
        // I changed the select structure to work up to m, instead of up to m - 1.

        _sel.Generate(selectVec, _maxVal >> (int)_remR, _valsRemsCount);
    }

    public uint Query(uint idx)
    {
        uint selRes, rank;

        if (idx > _maxVal)
            return _valsRemsCount;

        uint valQuot = idx >> (int)_remR;
        uint remsMask = (1U << (int)_remR) - 1U;
        uint valRem = idx & remsMask;

        if (valQuot == 0)
            rank = selRes = 0;
        else
        {
            selRes = _sel.Query(valQuot - 1) + 1;
            rank = selRes - valQuot;
        }

        //Genbox: Converted this from do+while to just while
        while (true)
        {
            if (BitBool.GetBit(_sel.BitsVec, selRes))
                break;

            if (BitBool.GetBitsValue(_valsRems, rank, _remR, remsMask) >= valRem)
                break;

            selRes++;
            rank++;
        }

        return rank;
    }

    public uint GetPackedSize()
    {
        return sizeof(uint) + //_maxVal
               sizeof(uint) + //_remR
               sizeof(uint) + //_valsRemsCount
               sizeof(uint) + //_valsRems length
               sizeof(uint) * (uint)_valsRems.Length + //_valsRems
               _sel.GetPackedSize();
    }

    public void Pack(Span<byte> buffer)
    {
        SpanWriter sw = new SpanWriter(buffer);
        sw.WriteUInt32(_maxVal);
        sw.WriteUInt32(_remR);
        sw.WriteUInt32(_valsRemsCount);
        sw.WriteUInt32((uint)_valsRems.Length);

        foreach (uint u in _valsRems)
            sw.WriteUInt32(u);

        buffer = buffer[sw.BytesWritten()..];
        _sel.Pack(buffer);
    }

    public static CompressedRank Unpack(ReadOnlySpan<byte> data)
    {
        SpanReader sr = new SpanReader(data);
        uint maxVal = sr.ReadUInt32();
        uint remR = sr.ReadUInt32();
        uint valsRemsCount = sr.ReadUInt32();
        uint valsRemsLength = sr.ReadUInt32();

        uint[] valsRems = new uint[valsRemsLength];

        for (int i = 0; i < valsRems.Length; i++)
            valsRems[i] = sr.ReadUInt32();

        data = data[sr.BytesRead()..];
        Select sel = Select.Unpack(data);

        return new CompressedRank(maxVal, remR, valsRemsCount, valsRems, sel);
    }
}