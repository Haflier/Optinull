using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06Search55DiagnosticTests
{
    [Fact]
    public void SearchForKnownOptimum()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var generator =
            new JobShopSequenceGenerator();

        var solver =
            new JobShopIteratedLocalSearchSolver(
                perturbationSwapCount: 3,
                iterationCount: 500,
                random: new Random(42));

        var evaluator =
            new JobShopEvaluator();

        var bestMakespan =
            double.MaxValue;

        JobSequence? bestSequence = null;

        for (var run = 0; run < 20; run++)
        {
            var random =
                new Random(1000 + run);

            var initialSequence =
                generator.Generate(
                    problem,
                    random);

            var result =
                solver.SolveSequence(
                    problem,
                    initialSequence);

            var makespan =
                evaluator
                    .Evaluate(
                        problem,
                        result)
                    .ObjectiveValue;

            Console.WriteLine(
                $"Run {run + 1}: {makespan}");

            if (makespan < bestMakespan)
            {
                bestMakespan =
                    makespan;

                bestSequence =
                    result;

                Console.WriteLine(
                    $"NEW BEST: {bestMakespan}");

                Console.WriteLine(
                    $"Sequence: " +
                    $"[{string.Join(
                        ", ",
                        result.JobIds)}]");
            }

            if (bestMakespan <= 55)
                break;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Best makespan found: {bestMakespan}");

        if (bestSequence is not null)
        {
            Console.WriteLine(
                $"Best sequence: " +
                $"[{string.Join(
                    ", ",
                    bestSequence.JobIds)}]");
        }

        Assert.True(
            bestMakespan <= 55,
            $"Did not find the known FT06 optimum. " +
            $"Best was {bestMakespan}.");
    }
}
