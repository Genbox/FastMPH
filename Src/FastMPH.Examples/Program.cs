using Genbox.FastMPH.CHD;
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

        if (!builder.TryCreateMinimal(data, out ChdMinimalState<string>? state))
        {
            Console.WriteLine("Unable to create perfect hash function");
            return;
        }

        foreach (string item in data)
            Console.WriteLine($"Hashcode for {item}: {state.Search(item)}");

        Console.WriteLine($"It packs to a function that uses {state.GetPackedSize() / (float)data.Length} bytes pr. element");
    }
}