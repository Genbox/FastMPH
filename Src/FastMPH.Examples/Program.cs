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

        if (!builder.TryCreate(data, out var state))
        {
            Console.WriteLine("Unable to create perfect hash function");
            return;
        }

        foreach (string item in data)
        {
            Console.WriteLine($"Hashcode for {item}: {state.Search(item)}");
        }
    }
}