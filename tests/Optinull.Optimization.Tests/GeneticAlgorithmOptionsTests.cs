using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Optimization.Tests;

public sealed class GeneticAlgorithmOptionsTests
{
    private static OptimizationSearchProblem CreateProblem()
    {
        var problem = new OptimizationProblem("max-sum");
        var x = problem.AddIntegerVariable("x", 0, 10);
        var y = problem.AddIntegerVariable("y", 0, 10);
        problem.Maximize(x + y);

        return new OptimizationSearchProblem(problem);
    }

    [Fact]
    public void MaxEvaluations_StopsTheSearchAtTheBudget()
    {
        var result = new GeneticAlgorithm<Solution>(
                populationSize: 20,
                generations: 10_000,
                maxEvaluations: 100,
                random: new RandomSource(1))
            .Solve(CreateProblem());

        Assert.Equal(100, result.EvaluationCount);
    }

    [Fact]
    public void MaxEvaluations_AlsoBoundsLocalSearch()
    {
        var result = new GeneticAlgorithm<Solution>(
                populationSize: 10,
                generations: 10_000,
                localSearchTries: 50,
                maxEvaluations: 200,
                random: new RandomSource(2))
            .Solve(CreateProblem());

        Assert.True(result.EvaluationCount <= 200 + 10 * 51);
        Assert.True(result.EvaluationCount >= 200);
    }

    [Fact]
    public void LocalSearch_FindsOptimum()
    {
        var result = new GeneticAlgorithm<Solution>(
                populationSize: 10,
                generations: 20,
                localSearchTries: 5,
                random: new RandomSource(3))
            .Solve(CreateProblem());

        Assert.Equal(20, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void LocalSearch_CostsExtraEvaluationsPerIndividual()
    {
        var plain = new GeneticAlgorithm<Solution>(
                populationSize: 10, generations: 5, eliteCount: 1,
                random: new RandomSource(4))
            .Solve(CreateProblem());

        var memetic = new GeneticAlgorithm<Solution>(
                populationSize: 10, generations: 5, eliteCount: 1,
                localSearchTries: 3,
                random: new RandomSource(4))
            .Solve(CreateProblem());

        Assert.True(memetic.EvaluationCount > plain.EvaluationCount);
    }

    [Fact]
    public void Constructor_RejectsInvalidNewArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneticAlgorithm<Solution>(localSearchTries: -1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeneticAlgorithm<Solution>(maxEvaluations: 0));
    }
}
