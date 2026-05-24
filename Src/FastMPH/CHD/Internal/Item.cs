using System.Runtime.InteropServices;

namespace Genbox.FastMPH.CHD.Internal;

[StructLayout(LayoutKind.Auto)]
internal struct Item
{
    public uint F;
    public uint H;
}