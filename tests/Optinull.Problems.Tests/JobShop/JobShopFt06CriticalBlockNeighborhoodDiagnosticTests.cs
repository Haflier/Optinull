using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06CriticalBlockNeighborhoodDiagnosticTests
{
    [Fact]
    public void InspectCriticalBlockNeighbors()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var sequence =
            new JobSequence(
            [
                2, 0, 1, 2, 1, 1, 2, 5, 5, 3, 3, 5,
                4, 0, 2, 1, 3, 3, 5, 4, 5, 4, 1, 0,
                5, 4, 0, 1, 2, 3, 3, 0, 2, 4, 0, 4
            ]);

        var evaluator =
            new JobShopEvaluator();

        var generator =
            new JobShopCriticalBlockNeighborhoodGenerator();

        var neighbors =
            generator
                .Generate(
                    problem,
                    sequence)
                .Select(
                    neighbor =>
                        new
                        {
                            Sequence = neighbor,
                            Makespan =
                                evaluator
                                    .Evaluate(
                                        problem,
                                        neighbor)
                                    .ObjectiveValue
                        })
                .ToList();

        Console.WriteLine(
            $"Original makespan: " +
            $"{evaluator.Evaluate(problem, sequence).ObjectiveValue}");

        Console.WriteLine(
            $"Critical-block neighbors: " +
            $"{neighbors.Count}");

        foreach (var neighbor in neighbors)
        {
            Console.WriteLine(
                $"Makespan: {neighbor.Makespan}, " +
                $"Sequence: " +
                $"[{string.Join(", ", neighbor.Sequence.JobIds)}]");
        }

        Assert.NotEmpty(neighbors);
    }
}
