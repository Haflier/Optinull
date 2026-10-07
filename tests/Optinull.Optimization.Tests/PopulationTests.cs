using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Genetics;

namespace Optinull.Optimization.Tests;

public class PopulationTests
{
    [Fact]
    public void Constructor_StoresSolutions()
    {
        var solution1 = new Solution();
        var solution2 = new Solution();

        var population = new Population(
            [solution1, solution2]);

        Assert.Equal(2, population.Count);

        Assert.Same(
            solution1,
            population.Solutions[0]);

        Assert.Same(
            solution2,
            population.Solutions[1]);
    }

    [Fact]
    public void Constructor_EmptyPopulation_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Population([]));
    }

    [Fact]
    public void Add_IncreasesPopulationSize()
    {
        var population = new Population(
            [new Solution()]);

        var solution = new Solution();

        population.Add(solution);

        Assert.Equal(2, population.Count);
        Assert.Same(
            solution,
            population.Solutions[1]);
    }
}
