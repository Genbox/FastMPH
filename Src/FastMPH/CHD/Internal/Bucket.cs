using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Genbox.FastMPH.CHD.Internal;

[StructLayout(LayoutKind.Auto)]
internal struct Bucket
{
    public uint ItemsList; // offset
    public uint Size;

    [SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented")]
    public uint BucketId
    {
        get => Size;
        set => Size = value;
    }
}