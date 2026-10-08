using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.JobShop.Experiments;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06IlsBenchmarkDiagnosticTests
{
    [Fact]
    public void RunMultiple_Ft06BenchmarkWithIls()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0, 0, 0, 0,
                1, 1, 1, 1, 1, 1,
                2, 2, 2, 2, 2, 2,
                3, 3, 3, 3, 3, 3,
                4, 4, 4, 4, 4, 4,
                5, 5, 5, 5, 5, 5
            ]);

        var runner =
            new JobShopExperimentRunner();

        var summaries =
            runner.RunMultiple(
                problem,
                initialSequence,
                runCount: 5,
                knownOptimalMakespan: 55,
                startingRandomSeed: 42);

        foreach (var summary in summaries)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {summary.SolverName} ===");
            Console.WriteLine(
                $"Best makespan:    {summary.BestMakespan}");
            Console.WriteLine(
                $"Worst makespan:   {summary.WorstMakespan}");
            Console.WriteLine(
                $"Average makespan: {summary.AverageMakespan:F2}");
            Console.WriteLine(
                $"Success rate:     {summary.SuccessRate:P2}");
            Console.WriteLine(
                $"Average gap:      {summary.AverageOptimalityGap:P2}");
            Console.WriteLine(
                $"Best time:        {summary.BestElapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine(
                $"Worst time:       {summary.WorstElapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine(
                $"Average time:     {summary.AverageElapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine(
                $"Average evaluations: {summary.AverageEvaluationCount:F0}");
        }

        Assert.Equal(4, summaries.Count);

        Assert.All(
            summaries,
            summary =>
            {
                Assert.True(summary.BestMakespan >= 55);
                Assert.True(
                    summary.WorstMakespan >=
                    summary.BestMakespan);

                Assert.True(
                    summary.AverageMakespan >=
                    summary.BestMakespan);
            });
    }
}
