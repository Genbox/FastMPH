using Genbox.FastMPH.CHD;
using Genbox.FastMPH.Internals;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genbox.FastMPH.Examples;

internal static class Program
{
    private static void Main(string[] args)
    {
        ChdBuilder<string> builder = new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance);

        string[] data =
        [
            "elephant",
            "goat",
            "horse",
            "cow"
        ];

        HashFunc<string> hashFunc = static (value, seed) => unchecked((ulong)value.GetHashCode(StringComparison.Ordinal) ^ seed);

        if (!builder.TryCreateMinimalWithRetry(data, hashFunc, out ChdMinimalState<string>? state))
        {
            Console.WriteLine("Unable to create perfect hash function");
            return;
        }

        foreach (string item in data)
            Console.WriteLine($"Hashcode for {item}: {state.Search(item)}");

        Console.WriteLine($"It packs to a function that uses {state.GetPackedSize() / (float)data.Length} bytes pr. element");
    }
}