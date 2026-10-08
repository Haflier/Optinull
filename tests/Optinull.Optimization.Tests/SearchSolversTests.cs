using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Optimization.Tests;

public sealed class SearchSolversTests
{
    // maximize x + y, both integers in [0, 10]: optimum is 20.
    private static OptimizationSearchProblem CreateProblem()
    {
        var problem = new OptimizationProblem("max-sum");
        var x = problem.AddIntegerVariable("x", 0, 10);
        var y = problem.AddIntegerVariable("y", 0, 10);
        problem.Maximize(x + y);

        return new OptimizationSearchProblem(problem);
    }

    [Fact]
    public void HillClimbing_Maximize_ReachesUpperBounds()
    {
        var result = new HillClimbing<Solution>(new RandomSource(1))
            .Solve(CreateProblem());

        Assert.Equal(20, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void SimulatedAnnealing_Maximize_FindsOptimum()
    {
        var result = new SimulatedAnnealing<Solution>(random: new RandomSource(7))
            .Solve(CreateProblem());

        Assert.Equal(20, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void IteratedLocalSearch_Maximize_FindsOptimum()
    {
        var result = new IteratedLocalSearch<Solution>(
                iterationCount: 5,
                random: new RandomSource(3))
            .Solve(CreateProblem());

        Assert.Equal(20, result.Evaluation.ObjectiveValue);
    }

    [Fact]
    public void Result_RecordsEvaluationsAndConvergence()
    {
        var result = new SimulatedAnnealing<Solution>(random: new RandomSource(7))
            .Solve(CreateProblem());

        Assert.True(result.EvaluationCount > 0);
        Assert.NotEmpty(result.Convergence);
        Assert.Equal(
            result.Evaluation.ObjectiveValue,
            result.Convergence[^1].Objective);
    }

    [Fact]
    public void SimulatedAnnealing_SameSeed_IsReproducible()
    {
        var first = new SimulatedAnnealing<Solution>(random: new RandomSource(11))
            .Solve(CreateProblem());

        var second = new SimulatedAnnealing<Solution>(random: new RandomSource(11))
            .Solve(CreateProblem());

        Assert.Equal(first.EvaluationCount, second.EvaluationCount);
        Assert.Equal(first.Convergence, second.Convergence);
    }

    [Fact]
    public void Solve_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new SimulatedAnnealing<Solution>(random: new RandomSource(1))
                .Solve(CreateProblem(), cts.Token));
    }
}
