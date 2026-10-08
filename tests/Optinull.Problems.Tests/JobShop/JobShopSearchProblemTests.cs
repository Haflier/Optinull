using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopSearchProblemTests
{
    private static JobShopSearchProblem Ft06() =>
        new(JobShopBenchmarkInstances.Ft06());

    [Fact]
    public void CreateRandom_HasSixOfEachJob()
    {
        var sequence = Ft06().CreateRandom(new RandomSource(1));

        Assert.Equal(36, sequence.Count);
        Assert.All(
            sequence.JobIds.GroupBy(id => id),
            group => Assert.Equal(6, group.Count()));
    }

    [Fact]
    public void RandomNeighbor_SwapsTwoPositionsAndKeepsCounts()
    {
        var problem = Ft06();
        var random = new RandomSource(2);
        var start = problem.CreateRandom(random);

        var neighbor = problem.RandomNeighbor(start, random);

        var changed = start.JobIds
            .Zip(neighbor.JobIds, (a, b) => a != b)
            .Count(different => different);

        Assert.Equal(2, changed);
        Assert.Equal(
            start.JobIds.OrderBy(id => id),
            neighbor.JobIds.OrderBy(id => id));
    }

    [Fact]
    public void Evaluate_KnownOptimalSequence_Gives55()
    {
        var sequence = new JobSequence(
        [
            2, 1, 2, 3, 0, 1, 4, 5, 5, 0, 3, 2,
            2, 5, 0, 3, 3, 0, 4, 1, 4, 1, 3, 0,
            5, 5, 5, 1, 2, 4, 0, 4, 2, 3, 1, 4
        ]);

        Assert.Equal(55, Ft06().Evaluate(sequence).ObjectiveValue);
    }

    [Fact]
    public void SimulatedAnnealing_OnFt06_FindsOptimum()
    {
        var problem = Ft06();

        var best = Enumerable.Range(1, 5)
            .Select(seed =>
                new SimulatedAnnealing<JobSequence>(
                        iterationsPerTemperature: 90,
                        random: new RandomSource(seed))
                    .Solve(problem)
                    .Evaluation.ObjectiveValue)
            .Min();

        Assert.Equal(55, best);
    }

    [Fact]
    public void SimulatedAnnealing_SameSeed_IsReproducible()
    {
        var problem = Ft06();

        var first = new SimulatedAnnealing<JobSequence>(
                random: new RandomSource(5)).Solve(problem);

        var second = new SimulatedAnnealing<JobSequence>(
                random: new RandomSource(5)).Solve(problem);

        Assert.Equal(first.Best.JobIds, second.Best.JobIds);
        Assert.Equal(first.EvaluationCount, second.EvaluationCount);
    }
}
