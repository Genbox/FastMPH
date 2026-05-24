using System.Runtime.InteropServices;

namespace Genbox.FastMPH.CHD.Internal;

[StructLayout(LayoutKind.Auto)]
internal struct SortedList
{
    public uint BucketList;
    public uint Size;
}