using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopRecombinationTests
{
    private static JobShopSearchProblem Ft06() =>
        new(JobShopBenchmarkInstances.Ft06());

    [Fact]
    public void Crossover_PreservesJobCountsAndLength()
    {
        var problem = Ft06();
        var random = new RandomSource(3);

        for (var i = 0; i < 100; i++)
        {
            var a = problem.CreateRandom(random);
            var b = problem.CreateRandom(random);

            var child = problem.Crossover(a, b, random);

            Assert.Equal(36, child.Count);
            Assert.Equal(
                a.JobIds.OrderBy(id => id),
                child.JobIds.OrderBy(id => id));
        }
    }

    [Fact]
    public void Crossover_RejectsDifferentLengths()
    {
        var problem = Ft06();

        Assert.Throws<ArgumentException>(() =>
            problem.Crossover(
                new JobSequence([0, 1]),
                new JobSequence([0, 1, 1]),
                new RandomSource(1)));
    }

    [Fact]
    public void Mutate_KeepsSequenceValid()
    {
        var problem = Ft06();
        var random = new RandomSource(5);
        var start = problem.CreateRandom(random);

        var mutated = problem.Mutate(start, random);

        Assert.Equal(
            start.JobIds.OrderBy(id => id),
            mutated.JobIds.OrderBy(id => id));
    }

    [Fact]
    public void GeneticAlgorithm_OnFt06_ReturnsGoodValidSchedule()
    {
        var problem = Ft06();

        var result = new GeneticAlgorithm<JobSequence>(
                populationSize: 50,
                generations: 200,
                eliteCount: 2,
                random: new RandomSource(42))
            .Solve(problem);

        Assert.True(result.Evaluation.ObjectiveValue <= 75);
        Assert.True(result.Evaluation.ObjectiveValue >= 55);
    }
}
