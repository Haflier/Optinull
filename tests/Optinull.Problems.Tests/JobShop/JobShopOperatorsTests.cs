using Optinull.Optimization.Randomness;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopOperatorsTests
{
    private static JobShopSearchProblem Create(
        JobShopCrossover crossover = JobShopCrossover.OnePoint,
        JobShopMutation mutation = JobShopMutation.Swap) =>
        new(
            JobShopBenchmarkInstances.Ft06(),
            crossover: crossover,
            mutation: mutation);

    [Theory]
    [InlineData(JobShopCrossover.OnePoint)]
    [InlineData(JobShopCrossover.PrecedencePreserving)]
    public void Crossover_PreservesJobCountsAndLength(JobShopCrossover kind)
    {
        var problem = Create(crossover: kind);
        var random = new RandomSource(11);

        for (var i = 0; i < 200; i++)
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
    public void Pox_KeepsSomeEntriesInPlaceFromTheFirstParent()
    {
        var problem = Create(crossover: JobShopCrossover.PrecedencePreserving);
        var random = new RandomSource(12);

        var a = problem.CreateRandom(random);
        var b = problem.CreateRandom(random);

        var child = problem.Crossover(a, b, random);

        var samePositionAsA = a.JobIds
            .Zip(child.JobIds, (x, y) => x == y)
            .Count(equal => equal);

        Assert.True(samePositionAsA > 0);
    }

    [Fact]
    public void Pox_OfIdenticalParents_ReturnsSameSequence()
    {
        var problem = Create(crossover: JobShopCrossover.PrecedencePreserving);
        var random = new RandomSource(13);

        var a = problem.CreateRandom(random);

        var child = problem.Crossover(a, a, random);

        Assert.Equal(a.JobIds, child.JobIds);
    }

    [Fact]
    public void Crossover_DoesNotModifyParents()
    {
        var problem = Create(crossover: JobShopCrossover.PrecedencePreserving);
        var random = new RandomSource(14);

        var a = problem.CreateRandom(random);
        var b = problem.CreateRandom(random);

        var aBefore = a.JobIds.ToArray();
        var bBefore = b.JobIds.ToArray();

        problem.Crossover(a, b, random);

        Assert.Equal(aBefore, a.JobIds);
        Assert.Equal(bBefore, b.JobIds);
    }

    [Theory]
    [InlineData(JobShopMutation.Swap)]
    [InlineData(JobShopMutation.Insertion)]
    public void Mutate_PreservesJobCountsAndChangesTheSequence(JobShopMutation kind)
    {
        var problem = Create(mutation: kind);
        var random = new RandomSource(15);

        for (var i = 0; i < 100; i++)
        {
            var start = problem.CreateRandom(random);

            var mutated = problem.Mutate(start, random);

            Assert.Equal(
                start.JobIds.OrderBy(id => id),
                mutated.JobIds.OrderBy(id => id));

            Assert.NotEqual(start.JobIds, mutated.JobIds);
        }
    }

    [Fact]
    public void Defaults_AreOnePointAndSwap_ForBackwardCompatibility()
    {
        var problem = new JobShopSearchProblem(JobShopBenchmarkInstances.Ft06());
        var explicitDefaults = Create();

        var a = problem.CreateRandom(new RandomSource(1));
        var b = problem.CreateRandom(new RandomSource(2));

        Assert.Equal(
            problem.Crossover(a, b, new RandomSource(9)).JobIds,
            explicitDefaults.Crossover(a, b, new RandomSource(9)).JobIds);
    }
}
