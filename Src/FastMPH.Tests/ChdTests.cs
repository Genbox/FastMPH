using Genbox.FastMPH.CHD;
using Genbox.FastMPH.Internals;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Tests;

public class ChdTests
{
    private static readonly HashFunc<int> _intHash = static (value, seed) => unchecked((ulong)value * seed);

    [Fact]
    public void MinimalBuilderRejectsKeysPerBinAboveOne()
    {
        int[] values = Enumerable.Range(0, 128).ToArray();
        ChdBuilder<int> builder = new ChdBuilder<int>(NullLogger<ChdBuilder<int>>.Instance);
        ChdMinimalSettings settings = new ChdMinimalSettings { KeysPerBin = 2 };

        Assert.False(builder.TryCreateMinimalWithRetry(values, _intHash, out ChdMinimalState<int>? _, settings));
    }
}
