using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt06SearchOptimumTests
{
    [Fact]
    public void SearchForKnownOptimum()
    {
        var problem = new JobShopSearchProblem(
            JobShopBenchmarkInstances.Ft06());

        var bestMakespan = double.MaxValue;
        JobSequence? bestSequence = null;

        for (var run = 0; run < 20; run++)
        {
            var start = problem.CreateRandom(new RandomSource(1000 + run));

            var solver = new IteratedLocalSearch<JobSequence>(
                perturbationSize: 3,
                iterationCount: 100,
                random: new RandomSource(42 + run));

            var result = solver.Solve(problem, start);
            var makespan = result.Evaluation.ObjectiveValue;

            Console.WriteLine($"Run {run + 1}: {makespan}");

            if (makespan < bestMakespan)
            {
                bestMakespan = makespan;
                bestSequence = result.Best;
            }

            if (bestMakespan <= 55)
                break;
        }

        Console.WriteLine($"Best makespan found: {bestMakespan}");

        if (bestSequence is not null)
        {
            Console.WriteLine(
                $"Best sequence: [{string.Join(", ", bestSequence.JobIds)}]");
        }

        Assert.True(
            bestMakespan <= 55,
            $"Did not find the known FT06 optimum. Best was {bestMakespan}.");
    }
}
