using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06LocalOptimumDiagnosticTests
{
    [Fact]
    public void InspectBestSwapNeighbors()
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

        var original =
            evaluator.Evaluate(
                problem,
                sequence);

        Console.WriteLine(
            $"Original makespan: {original.ObjectiveValue}");

        var results =
            new List<NeighborResult>();

        var jobIds =
            sequence.JobIds.ToArray();

        for (var firstIndex = 0;
             firstIndex < jobIds.Length - 1;
             firstIndex++)
        {
            for (var secondIndex = firstIndex + 1;
                 secondIndex < jobIds.Length;
                 secondIndex++)
            {
                if (jobIds[firstIndex] ==
                    jobIds[secondIndex])
                {
                    continue;
                }

                var neighbor =
                    jobIds.ToArray();

                (
                    neighbor[firstIndex],
                    neighbor[secondIndex]
                ) =
                (
                    neighbor[secondIndex],
                    neighbor[firstIndex]
                );

                var neighborSequence =
                    new JobSequence(neighbor);

                var makespan =
                    evaluator
                        .Evaluate(
                            problem,
                            neighborSequence)
                        .ObjectiveValue;

                results.Add(
                    new NeighborResult(
                        firstIndex,
                        secondIndex,
                        jobIds[firstIndex],
                        jobIds[secondIndex],
                        makespan));
            }
        }

        Console.WriteLine();
        Console.WriteLine("Best 20 swap neighbors:");

        foreach (var result in results
                     .OrderBy(result => result.Makespan)
                     .Take(20))
        {
            Console.WriteLine(
                $"Positions {result.FirstIndex} <-> " +
                $"{result.SecondIndex}: " +
                $"J{result.FirstJobId} <-> " +
                $"J{result.SecondJobId}, " +
                $"makespan = {result.Makespan}");
        }
    }

    private sealed record NeighborResult(
        int FirstIndex,
        int SecondIndex,
        int FirstJobId,
        int SecondJobId,
        double Makespan);
}
