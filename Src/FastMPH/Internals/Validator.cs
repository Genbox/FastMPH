using System.Diagnostics.CodeAnalysis;

namespace Genbox.FastMPH.Internals;

internal static class Validator
{
    public static void RequireThat([DoesNotReturnIf(false)]bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("Condition failed");
    }
}