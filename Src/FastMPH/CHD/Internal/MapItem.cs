using System.Runtime.InteropServices;

namespace Genbox.FastMPH.CHD.Internal;

[StructLayout(LayoutKind.Auto)]
internal struct MapItem
{
    public uint BucketNum;
    public uint F;
    public uint H;
}