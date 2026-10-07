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

    [Fact]
    public void Rank_Maximize_PlacesHighestObjectiveFirst()
    {
        var problem = new OptimizationProblem(
            "Ranking");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            100);

        problem.Maximize(x);

        var comparison = new SolverComparison();

        var results = new[]
        {
            CreateResult("Solver A", 20),
            CreateResult("Solver B", 50),
            CreateResult("Solver C", 30)
        };

        var ranking = comparison.Rank(
            problem,
            results);

        Assert.Equal(
            "Solver B",
            ranking[0].Result.SolverName);

        Assert.Equal(
            "Solver C",
            ranking[1].Result.SolverName);

        Assert.Equal(
            "Solver A",
            ranking[2].Result.SolverName);

        Assert.Equal(1, ranking[0].Rank);
        Assert.Equal(2, ranking[1].Rank);
        Assert.Equal(3, ranking[2].Rank);
    }

    [Fact]
    public void Rank_Minimize_PlacesLowestObjectiveFirst()
    {
        var problem = new OptimizationProblem(
            "Ranking");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            100);

        problem.Minimize(x);

        var comparison = new SolverComparison();

        var results = new[]
        {
            CreateResult("Solver A", 20),
            CreateResult("Solver B", 5),
            CreateResult("Solver C", 10)
        };

        var ranking = comparison.Rank(
            problem,
            results);

        Assert.Equal(
            "Solver B",
            ranking[0].Result.SolverName);

        Assert.Equal(
            "Solver C",
            ranking[1].Result.SolverName);

        Assert.Equal(
            "Solver A",
            ranking[2].Result.SolverName);
    }

    [Fact]
    public void Rank_EqualObjectives_ProducesSameRank()
    {
        var problem = new OptimizationProblem(
            "Ranking");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            100);

        problem.Maximize(x);

        var comparison = new SolverComparison();

        var results = new[]
        {
            CreateResult("Solver A", 50),
            CreateResult("Solver B", 50),
            CreateResult("Solver C", 20)
        };

        var ranking = comparison.Rank(
            problem,
            results);

        Assert.Equal(
            ranking[0].Rank,
            ranking[1].Rank);

        Assert.Equal(1, ranking[0].Rank);
        Assert.Equal(1, ranking[1].Rank);
        Assert.Equal(3, ranking[2].Rank);
    }

    private static SolverResult CreateResult(
        string solverName,
        double objectiveValue)
    {
        var problem = new OptimizationProblem(
            "Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            100);

        problem.Maximize(x);

        var solution = new Optinull.Domain.Solutions.Solution();

        solution.SetValue(
            x,
            objectiveValue);

        var evaluation = new Optinull.Domain.Evaluation.EvaluationResult(
            objectiveValue,
            isFeasible: true,
            penalty: 0);

        var optimizationResult =
            new OptimizationResult(
                solution,
                evaluation);

        return new SolverResult(
            solverName,
            optimizationResult,
            TimeSpan.FromMilliseconds(1));
    }
}
