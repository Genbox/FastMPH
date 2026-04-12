using BenchmarkDotNet.Running;

namespace Genbox.FastMPH.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}