using Optinull.Application.Optimization;
using Optinull.Problems.Knapsack;

namespace Optinull.Application.Tests;

public sealed class KnapsackSolveServiceTests
{
    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void Knap20_ReturnsAFeasibleSelection_CloseToTheOptimum(SolverKind kind)
    {
        var problem = KnapsackInstances.Random(20, seed: 11);
        var optimum = KnapsackExactSolver.Optimum(problem);

        var result = new KnapsackSolveService().Solve(problem, kind, seed: 42);

        Assert.True(result.Weight <= problem.Capacity);
        Assert.Equal(result.Value, result.Search.Evaluation.ObjectiveValue);
        Assert.True(result.Value <= optimum);
        Assert.True(
            result.Value >= optimum * 0.9,
            $"{kind}: expected at least 90% of {optimum}, got {result.Value}.");
    }

    [Fact]
    public void SimulatedAnnealing_OnLargerInstance_DoesNotLoseToGreedy()
    {
        var problem = KnapsackInstances.Random(100, seed: 11);

        var result = new KnapsackSolveService().Solve(problem, SolverKind.SimulatedAnnealing);

        Assert.True(result.Value >= result.GreedyValue * 0.95);
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing, 900, 800, 1100)]
    [InlineData(SolverKind.GeneticAlgorithm, 500, 100, 500)]
    public void Iterations_LimitTheNumberOfEvaluations(
        SolverKind kind,
        int iterations,
        int atLeast,
        int atMost)
    {
        var problem = KnapsackInstances.Random(20, seed: 11);

        var result = new KnapsackSolveService().Solve(problem, kind, seed: 1, iterations: iterations);

        Assert.InRange(result.Search.EvaluationCount, atLeast, atMost);
    }

    [Fact]
    public void SameSeed_GivesTheSameResult()
    {
        var problem = KnapsackInstances.Random(30, seed: 2);
        var service = new KnapsackSolveService();

        Assert.Equal(
            service.Solve(problem, SolverKind.GeneticAlgorithm, seed: 9).Value,
            service.Solve(problem, SolverKind.GeneticAlgorithm, seed: 9).Value);
    }
}
