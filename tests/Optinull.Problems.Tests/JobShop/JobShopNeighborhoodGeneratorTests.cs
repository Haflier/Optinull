using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopNeighborhoodGeneratorTests
{
    [Fact]
    public void Generate_CreatesSwapNeighbors()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var generator =
            new JobShopNeighborhoodGenerator();

        var neighbors =
            generator.Generate(sequence)
                .ToList();

        Assert.NotEmpty(neighbors);

        Assert.Contains(
            neighbors,
            neighbor =>
                neighbor.JobIds.SequenceEqual(
                    [1, 0, 0, 1]));
    }

    [Fact]
    public void Generate_PreservesJobCounts()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var generator =
            new JobShopNeighborhoodGenerator();

        var neighbors =
            generator.Generate(sequence)
                .ToList();

        Assert.All(
            neighbors,
            neighbor =>
            {
                Assert.Equal(
                    2,
                    neighbor.JobIds.Count(id => id == 0));

                Assert.Equal(
                    2,
                    neighbor.JobIds.Count(id => id == 1));
            });
    }

    [Fact]
    public void Generate_DoesNotModifyOriginalSequence()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var original =
            sequence.JobIds.ToArray();

        var generator =
            new JobShopNeighborhoodGenerator();

        generator.Generate(sequence)
            .ToList();

        Assert.Equal(
            original,
            sequence.JobIds);
    }

    [Fact]
    public void Generate_DoesNotCreateNoOpSwaps()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var generator =
            new JobShopNeighborhoodGenerator();

        var neighbors =
            generator.Generate(sequence)
                .ToList();

        Assert.DoesNotContain(
            neighbors,
            neighbor =>
                neighbor.JobIds.SequenceEqual(
                    sequence.JobIds));
    }

    [Fact]
    public void Generate_CreatesExpectedNumberOfUniqueNeighbors()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var generator =
            new JobShopNeighborhoodGenerator();

        var neighbors =
            generator.Generate(sequence)
                .Select(neighbor =>
                    string.Join(",", neighbor.JobIds))
                .ToList();

        Assert.Equal(
            neighbors.Count,
            neighbors.Distinct().Count());
    }
}
