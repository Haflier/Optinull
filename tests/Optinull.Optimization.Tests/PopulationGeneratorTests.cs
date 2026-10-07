using Optinull.Domain.Problems;
using Optinull.Optimization.Genetics;
using Optinull.Optimization.Generation;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Tests;

public class PopulationGeneratorTests
{
    [Fact]
    public void Generate_CreatesRequestedPopulationSize()
    {
        var problem = new OptimizationProblem(
            "Population");

        problem.AddIntegerVariable(
            "x",
            0,
            10);

        var generator = new PopulationGenerator(
            new RandomSolutionGenerator(),
            new RandomSource(42));

        var population = generator.Generate(
            problem,
            20);

        Assert.Equal(20, population.Count);
    }

    [Fact]
    public void Generate_ThrowsForInvalidSize()
    {
        var problem = new OptimizationProblem(
            "Population");

        var generator = new PopulationGenerator(
            new RandomSolutionGenerator(),
            new RandomSource(42));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => generator.Generate(problem, 0));
    }

    [Fact]
    public void Generate_ProducesValidSolutions()
    {
        var problem = new OptimizationProblem(
            "Population");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        var generator = new PopulationGenerator(
            new RandomSolutionGenerator(),
            new RandomSource(42));

        var population = generator.Generate(
            problem,
            10);

        foreach (var solution in population.Solutions)
        {
            var value = solution.GetValue(x);

            Assert.InRange(
                value,
                0,
                10);
        }
    }
}
