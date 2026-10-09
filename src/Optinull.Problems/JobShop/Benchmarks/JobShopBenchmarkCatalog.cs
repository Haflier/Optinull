using System.Diagnostics.CodeAnalysis;

namespace Optinull.Problems.JobShop.Benchmarks;

public sealed record JobShopBenchmark(
    string Name,
    JobShopProblem Problem,
    double KnownOptimum);

/// <summary>The built-in benchmark instances, looked up by name.</summary>
public static class JobShopBenchmarkCatalog
{
    public static IReadOnlyList<string> Names { get; } = ["ft06", "ft10"];

    public static bool TryGet(
        string name,
        [NotNullWhen(true)] out JobShopBenchmark? benchmark)
    {
        switch (name?.Trim().ToLowerInvariant())
        {
            case "ft06":
                benchmark = new JobShopBenchmark(
                    "ft06", JobShopBenchmarkInstances.Ft06(), 55);
                return true;

            case "ft10":
                benchmark = new JobShopBenchmark(
                    "ft10", JobShopBenchmarkInstances.Ft10(), 930);
                return true;

            default:
                benchmark = null;
                return false;
        }
    }
}
