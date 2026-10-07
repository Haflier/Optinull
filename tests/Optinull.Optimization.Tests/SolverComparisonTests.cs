using Optinull.Domain.Problems;
using Optinull.Optimization.Solvers;

namespace Optinull.Optimization.Tests;

public class SolverComparisonTests
{
    [Fact]
    public void Compare_RunsAllProvidedSolvers()
    {
        var problem = new OptimizationProblem(
            "Comparison");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Maximize(x);

        var comparison = new SolverComparison();

        var solvers =
            new (string Name, IOptimizationSolver Solver)[]
            {
                (
                    "Greedy",
                    new GreedySolver()),

                (
                    "Hill Climbing",
                    new HillClimbingSolver())
            };

        var results = comparison.Compare(
            problem,
            solvers);

        Assert.Equal(2, results.Count);

        Assert.Equal(
            "Greedy",
            results[0].SolverName);

        Assert.Equal(
            "Hill Climbing",
            results[1].SolverName);
    }

    [Fact]
    public void Compare_RecordsElapsedTime()
    {
        var problem = new OptimizationProblem(
            "Timing");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Maximize(x);

        var comparison = new SolverComparison();

        var results = comparison.Compare(
            problem,
            [
                (
                    "Greedy",
                    new GreedySolver()
                )
            ]);

        Assert.Single(results);

        Assert.True(
            results[0].Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Compare_ReturnsSolverSolutions()
    {
        var problem = new OptimizationProblem(
            "Solutions");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Maximize(x);

        var comparison = new SolverComparison();

        var results = comparison.Compare(
            problem,
            [
                (
                    "Greedy",
                    new GreedySolver()
                )
            ]);

        var result = Assert.Single(results);

        Assert.Equal(
            10,
            result.Solution.GetValue(x));
    }
}
