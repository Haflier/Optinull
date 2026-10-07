using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopSequenceCrossoverTests
{
    [Fact]
    public void Crossover_PreservesSequenceLength()
    {
        var firstParent =
            new JobSequence([0, 1, 0, 1]);

        var secondParent =
            new JobSequence([1, 0, 1, 0]);

        var crossover =
            new JobShopSequenceCrossover();

        var child =
            crossover.Crossover(
                firstParent,
                secondParent,
                new Random(42));

        Assert.Equal(
            firstParent.Count,
            child.Count);
    }

    [Fact]
    public void Crossover_PreservesJobIdCounts()
    {
        var firstParent =
            new JobSequence([0, 1, 0, 1]);

        var secondParent =
            new JobSequence([1, 0, 1, 0]);

        var crossover =
            new JobShopSequenceCrossover();

        var child =
            crossover.Crossover(
                firstParent,
                secondParent,
                new Random(42));

        Assert.Equal(
            firstParent.JobIds.Count(id => id == 0),
            child.JobIds.Count(id => id == 0));

        Assert.Equal(
            firstParent.JobIds.Count(id => id == 1),
            child.JobIds.Count(id => id == 1));
    }

    [Fact]
    public void Crossover_DoesNotModifyParents()
    {
        var firstParent =
            new JobSequence([0, 1, 0, 1]);

        var secondParent =
            new JobSequence([1, 0, 1, 0]);

        var firstBefore =
            firstParent.JobIds.ToArray();

        var secondBefore =
            secondParent.JobIds.ToArray();

        var crossover =
            new JobShopSequenceCrossover();

        _ = crossover.Crossover(
            firstParent,
            secondParent,
            new Random(42));

        Assert.Equal(
            firstBefore,
            firstParent.JobIds);

        Assert.Equal(
            secondBefore,
            secondParent.JobIds);
    }

    [Fact]
    public void Crossover_IsReproducibleWithSameSeed()
    {
        var firstParent =
            new JobSequence([0, 1, 0, 1]);

        var secondParent =
            new JobSequence([1, 0, 1, 0]);

        var crossover =
            new JobShopSequenceCrossover();

        var firstChild =
            crossover.Crossover(
                firstParent,
                secondParent,
                new Random(42));

        var secondChild =
            crossover.Crossover(
                firstParent,
                secondParent,
                new Random(42));

        Assert.Equal(
            firstChild.JobIds,
            secondChild.JobIds);
    }

    [Fact]
    public void Crossover_RejectsDifferentParentLengths()
    {
        var firstParent =
            new JobSequence([0, 1]);

        var secondParent =
            new JobSequence([0, 1, 0]);

        var crossover =
            new JobShopSequenceCrossover();

        Assert.Throws<ArgumentException>(
            () => crossover.Crossover(
                firstParent,
                secondParent,
                new Random(42)));
    }
}
