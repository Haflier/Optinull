using Optinull.Application.Optimization;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests;

public sealed class TspSolveServiceTests
{
    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Circle20_ReturnsAValidTourCloseToTheOptimum(SolverKind kind)
    {
        var problem = TspBenchmarkInstances.Circle(20);
        var optimum = TspBenchmarkInstances.CircleOptimum(20);

        var result = new TspSolveService().Solve(problem, kind, seed: 42);

        Assert.Equal(Enumerable.Range(0, 20), result.Tour.Cities.OrderBy(city => city));
        Assert.Equal(result.Length, result.Search.Evaluation.ObjectiveValue, 6);
        Assert.True(result.Length >= optimum - 1e-6);
        Assert.True(
            result.Length <= optimum * 1.10,
            $"{kind}: expected within 10% of {optimum:F1}, got {result.Length:F1}.");
    }

    [Fact]
    public void SimulatedAnnealing_OnRandomCities_BeatsNearestNeighbour()
    {
        var problem = TspBenchmarkInstances.RandomCities(50, seed: 7);

        var result = new TspSolveService().Solve(problem, SolverKind.SimulatedAnnealing);

        Assert.True(
            result.Length <= result.GreedyLength,
            $"SA {result.Length:F1} should not be worse than nearest neighbour {result.GreedyLength:F1}.");
    }

    [Fact]
    public void GeneticAlgorithm_OnRandomCities_ReturnsAValidTour()
    {
        var problem = TspBenchmarkInstances.RandomCities(50, seed: 7);

        var result = new TspSolveService().Solve(problem, SolverKind.GeneticAlgorithm);

        Assert.Equal(Enumerable.Range(0, 50), result.Tour.Cities.OrderBy(city => city));
        Assert.True(result.Search.EvaluationCount <= TspSolveService.EvaluationBudget);
    }

    [Fact]
    public void SameSeed_IsReproducible()
    {
        var problem = TspBenchmarkInstances.RandomCities(30, seed: 3);
        var service = new TspSolveService();

        Assert.Equal(
            service.Solve(problem, SolverKind.SimulatedAnnealing, seed: 9).Length,
            service.Solve(problem, SolverKind.SimulatedAnnealing, seed: 9).Length);
    }
}
