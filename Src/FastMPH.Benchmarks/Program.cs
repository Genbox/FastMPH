using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace Genbox.FastMPH.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        IConfig config = DefaultConfig.Instance.AddJob(new Job(InfrastructureMode.InProcess, RunMode.Short));
        // var config = new DebugInProcessConfig();
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}